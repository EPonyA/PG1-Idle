﻿using System;
using System.Threading;

namespace Source
{
    class Program
    {
        // Instantiation
        static public Agent[] agentObjsArr = new Agent[10]; // 10 agents
        static public Upgrade[] upgraObjsArr = new Upgrade[10]; // 10 upgrades
        static public RenderWindow renderObj = new RenderWindow(9, 5); // 10 rows, 6 columns

        static public int SharedResource { get; set; }
        static public object raceConditionLocker = 0;
        static public bool exitCheck; // checker for killing threads;
        static public MassiveNumber gamePoints = new MassiveNumber();

        // Update the game console.
        static public void UpdateConsole()
        {
            // Update the renderer's points.
            RenderWindow.gamePoints = gamePoints;

            for (int i = 0; i < 10; i++)
            {
                RenderWindow.agentCount[i] = agentObjsArr[i].count;
                RenderWindow.agentPrice[i] = agentObjsArr[i].GetPrice();
                RenderWindow.agentPointsRate[i] = agentObjsArr[i].pointsRate; // Used for optimal calculation

                RenderWindow.upgraCount[i] = upgraObjsArr[i].count;
                RenderWindow.upgraPrice[i] = upgraObjsArr[i].GetPrice();
                RenderWindow.upgraIncomeMult[i] = upgraObjsArr[i].incomeMultiplier; // Used for optimal calculation
            }

            renderObj.RenderLoop();
        }

        static public void UnlockAgents()
        {
            // Attempt to unlock every locked agent.
            for (int i = 0; i < agentObjsArr.Length; i++)
            {
                if (agentObjsArr[i].isLocked)
                {
                    agentObjsArr[i].Unlock(gamePoints);
                    RenderWindow.agentIsLocked[i] = agentObjsArr[i].isLocked;
                }
            }
        }

        // Method for game loop thread.
        #region

        static void GameLoop()
        {
            while (!exitCheck)
            {
                // Loop through all agents to increase the player's points bank.
                for (uint i = 0; i < 10; i++)
                {
                    if (!agentObjsArr[i].isLocked)
                    {
                        if (agentObjsArr[i].count.value > 0)
                        {
                            MassiveNumber tempNumber = new MassiveNumber();

                            // Evaluate agents.
                            tempNumber.value = tempNumber.Add(agentObjsArr[i].pointsRate.Mult((agentObjsArr[i].count.value), agentObjsArr[i].count.echelon), agentObjsArr[i].pointsRate.echelon);
                            tempNumber.UpdateEchelon();

                            // Evaluate upgrades.
                            if (upgraObjsArr[i].count.value > 0)
                            {
                                tempNumber.value = tempNumber.Mult(upgraObjsArr[i].count.Mult(upgraObjsArr[i].incomeMultiplier, upgraObjsArr[i].count.echelon) + 1, 1);
                                tempNumber.UpdateEchelon();
                            }

                            gamePoints.value = gamePoints.Add(tempNumber.value, tempNumber.echelon);
                            gamePoints.UpdateEchelon();
                        }
                    }
                }

                RenderWindow.gamePoints = gamePoints;
                gamePoints.UpdateEchelon();

                lock (raceConditionLocker)
                {
                    // Attempt to update the console.
                    UpdateConsole();
                    SharedResource++;

                    // Attempt to unlock every locked agent.
                    UnlockAgents();
                }

                // Loop every second.
                Thread.Sleep(1000);
            }
        }

        #endregion

        // Method for player input thread.
        #region

        static void PlayerInput()
        {
            while (!exitCheck)
            {
                int inputIndex;

                // Player controls
                while (Console.KeyAvailable)
                {
                    Console.ReadKey(false);
                }

                ConsoleKeyInfo playerInput = Console.ReadKey();

                if (Char.IsNumber(playerInput.KeyChar))
                {
                    // Get array index from digit input.
                    inputIndex = (int)(Char.GetNumericValue(playerInput.KeyChar) + 9) % 10;
                    MassiveNumber itemCost = new MassiveNumber();

                    if (RenderWindow.currentMenuInd == 1)
                    {
                        itemCost = agentObjsArr[inputIndex].GetPrice();
                    }
                    else if (RenderWindow.currentMenuInd == 2)
                    {
                        itemCost = upgraObjsArr[inputIndex].GetPrice();
                    }

                    // If the player has sufficient points
                    if (gamePoints.IsGreaterThan(itemCost))
                    {
                        // Update the console values.
                        if (RenderWindow.currentMenuInd == 1)
                        {
                            // Increment the agent that the user inputs.
                            agentObjsArr[inputIndex].count.value = agentObjsArr[inputIndex].count.Add(1, 1);
                            agentObjsArr[inputIndex].count.UpdateEchelon();
                            agentObjsArr[inputIndex].UpdatePrice();

                            // Decrease the player's points bank.
                            gamePoints.value = gamePoints.Sub(itemCost.value, itemCost.echelon);
                            gamePoints.UpdateEchelon();
                        }
                        else if (RenderWindow.currentMenuInd == 2)
                        {
                            // If the upgrade is available.
                            if (upgraObjsArr[inputIndex].count.value <= upgraObjsArr[inputIndex].maxCount)
                            {
                                // Increment the upgrade that the user inputs.
                                upgraObjsArr[inputIndex].count.value = upgraObjsArr[inputIndex].count.Add(1, 1);
                                upgraObjsArr[inputIndex].count.UpdateEchelon();
                                upgraObjsArr[inputIndex].UpdatePrice();

                                // Decrease the player's points bank.
                                gamePoints.value = gamePoints.Sub(itemCost.value, itemCost.echelon);
                                gamePoints.UpdateEchelon();
                            }
                        }
                    }
                }
                else
                {
                    switch (playerInput.Key)
                    {
                        case ConsoleKey.Spacebar:
                            gamePoints.value = gamePoints.Add(1, 1);
                            gamePoints.UpdateEchelon();

                            // Attempt to unlock every locked agent.
                            UnlockAgents();

                            break;

                        case ConsoleKey.S:
                            FileIO.SaveGame();
                            break;

                        case ConsoleKey.L:
                            FileIO.LoadGame();
                            break;

                        case ConsoleKey.RightArrow:
                            RenderWindow.ChangeMenu(RenderWindow.currentMenuInd + 1);
                            break;

                        case ConsoleKey.LeftArrow:
                            RenderWindow.ChangeMenu(RenderWindow.currentMenuInd - 1);
                            break;

                        case ConsoleKey.X:
                            exitCheck = true;

                            break;
                    }
                }

                // Prevent both threads from updating simultaneously.
                lock (raceConditionLocker)
                {
                    UpdateConsole();
                    SharedResource--;
                }

                Thread.Sleep(30);
            }

            if (exitCheck)
            {
                Console.Clear();
                Console.WriteLine("Thank you for playing! Your final total of money earned was: " + gamePoints.GetAbbreviation());
                Console.ReadKey();
            }
        }

        #endregion

        static void Main()
        {
            // Initialize the exit condition.
            exitCheck = false;

            // Initialize ten agents.
            agentObjsArr[0] = new Agent(1, 10, 1, 1.0275);
            agentObjsArr[1] = new Agent(5, 30, 1, 1.03);
            agentObjsArr[2] = new Agent(10, 70, 1, 1.035);
            agentObjsArr[3] = new Agent(20, 150, 1, 1.04);
            agentObjsArr[4] = new Agent(40, 350, 1, 1.0425);
            agentObjsArr[5] = new Agent(90, 500, 1, 1.044);
            agentObjsArr[6] = new Agent(150, 1, 2, 1.05);
            agentObjsArr[7] = new Agent(250, 2, 2, 1.055);
            agentObjsArr[8] = new Agent(500, 3.5, 2, 1.06);
            agentObjsArr[9] = new Agent(1000, 5, 2, 1.065);

            // Initialize ten upgrades.
            upgraObjsArr[0] = new Upgrade(1, 100, 1, 2);
            upgraObjsArr[1] = new Upgrade(2.5, 300, 1, 1.45);
            upgraObjsArr[2] = new Upgrade(5, 700, 1, 2.6);
            upgraObjsArr[3] = new Upgrade(10, 1.5, 2, 2.5);
            upgraObjsArr[4] = new Upgrade(20, 3.5, 2, 2.4);
            upgraObjsArr[5] = new Upgrade(45, 5, 2, 2.37);
            upgraObjsArr[6] = new Upgrade(85, 10, 2, 2.35);
            upgraObjsArr[7] = new Upgrade(150, 15, 2, 2.28);
            upgraObjsArr[8] = new Upgrade(250, 35, 2, 1.5);
            upgraObjsArr[9] = new Upgrade(300, 50, 2, 1.75);

            // Initial console draw.
            UpdateConsole();

            // Instantiate threads.
            ThreadStart gameLoop = new ThreadStart(GameLoop);
            Thread myGameLoop = new Thread(gameLoop);
            myGameLoop.Start();

            ThreadStart inputLoop = new ThreadStart(PlayerInput);
            Thread myInputLoop = new Thread(inputLoop);
            myInputLoop.Start();
        }
    }
}
