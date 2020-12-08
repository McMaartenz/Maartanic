﻿using System;
using System.Threading;
using System.Collections.Generic;

namespace Maartanic
{
	internal class ExtendedInstructions
	{

		internal bool recognizedInstruction = false;

		private readonly Dictionary<string, Func<string>> toBeAdded = new Dictionary<string, Func<string>>()
		{
			{ "pask", () => OutputForm.app.AskInput() }, // ask with gui interface, invoke on windowProcess thread
			{ "maartanic", () => "true" } // whether or not this is the Maartanic Engine
		};

		internal ExtendedInstructions()
		{
			foreach (KeyValuePair<string, Func<string>> x in toBeAdded) // extend predefined variables
			{
				Engine.predefinedVariables.Add(x.Key, x.Value);
			}
		}

		public void Dispose() // Garbage collect it when switching to compat (VSB)
		{
			foreach (KeyValuePair<string, Func<string>> x in toBeAdded)
			{
				Engine.predefinedVariables.Remove(x.Key);
			}
		}
		private static T Parse<T>(string input)
		{
			return Program.Parse<T>(input);
		}

		private bool InternalCompare(ref string[] compareIn, ref string[] lineInfo, ref Engine e)
		{
			string[] args = e.ExtractArgs(ref lineInfo);
			if (args.Length > 3)
			{
				compareIn[1] = args[2];
				compareIn[2] = args[3];
			}
			else
			{
				compareIn[1] = args[1];
				compareIn[2] = args[2];
			}
			return e.Compare(ref compareIn);
		}

		internal string Instructions(Engine e, ref string[] lineInfo, ref string[] args)
		{
			switch (lineInfo[0].ToUpper())
			{

				case "ENDF":
				case "ENDW":
				case "ENDDW":
					return e.lineIndex.ToString() + "." + e.returnedValue; // Return address to jump to later and the original return value separated by a dot.

				case "FOR": // FOR [script] [amount]	r-r
							// FOR [amount]				r		(+ENDF)
					if (args.Length > 1)
					{
						Engine forLoopEngine;
						int amount = Parse<int>(args[1]);
						bool selfRegulatedBreak = false;
						bool skipNext = false;
						for (int i = 0; i < amount; i++)
						{
							forLoopEngine = new Engine(e.scriptFile, args[0]);
							if (forLoopEngine.Executable())
							{
								if (!skipNext)
								{
									e.returnedValue = forLoopEngine.returnedValue = forLoopEngine.StartExecution();
								}
								else
								{
									skipNext = false;
								}
								if (forLoopEngine.returnedValue.StartsWith("3&"))
								{
									e.SendMessage(Engine.Level.INF, "Break statement");
									selfRegulatedBreak = true;
									break;
								}
								else if (forLoopEngine.returnedValue.StartsWith("4&"))
								{
									e.SendMessage(Engine.Level.INF, "Continue statement");
									skipNext = true;
									continue;
								}
							}
							else
							{
								if (!selfRegulatedBreak)
								{
									e.SendMessage(Engine.Level.ERR, "FOR statement failed to execute.");
								}
								else
								{
									e.SendMessage(Engine.Level.INF, "FOR self regulated loop break");
									e.returnedValue = forLoopEngine.returnedValue = forLoopEngine.returnedValue[(forLoopEngine.returnedValue.IndexOf('&') + 1)..];
								}
								e.StatementJumpOut("ENDF", "FOR");
							}
						}
					}
					else
					{
						Engine forEngine = new Engine(e.scriptFile)
						{
							localMemory = e.localMemory,            // Copy over local memory, and return
							returnedValue = e.returnedValue
						};
						int amount = Parse<int>(args[0]);
						bool selfRegulatedBreak = false;

						for (int i = 0; i < amount; i++)
						{
							if (i != 0)
							{
								if (forEngine.returnedValue.Contains('.'))
								{
									forEngine.returnedValue = forEngine.returnedValue[(forEngine.returnedValue.IndexOf('.') + 1)..];
								}
								else
								{
									if (forEngine.returnedValue.StartsWith('3') && forEngine.returnedValue[1] == '&')
									{
										e.SendMessage(Engine.Level.INF, "Break statement");
										selfRegulatedBreak = true;
										break;
									}

									if (forEngine.returnedValue == "4" && forEngine.returnedValue[1] == '&')
									{
										e.SendMessage(Engine.Level.INF, "Continue statement");
										forEngine.returnedValue = forEngine.StartExecution(true, e.lineIndex);
										continue;
									}

									e.SendMessage(Engine.Level.INF, "Return statement");
									return forEngine.returnedValue;
								}
							}
							forEngine.returnedValue = forEngine.StartExecution(true, e.lineIndex);
						}
						e.localMemory = forEngine.localMemory; // Copy back
						if (forEngine.returnedValue.Contains('.'))
						{
							int jumpLine = Parse<int>(forEngine.returnedValue[..forEngine.returnedValue.IndexOf('.')]);
							e.returnedValue = forEngine.returnedValue[(forEngine.returnedValue.IndexOf('.') + 1)..];
							e.JumpToLine(ref e.sr, ref e.line, ref e.lineIndex, ref jumpLine);
						}
						else
						{
							if (!selfRegulatedBreak)
							{
								e.SendMessage(Engine.Level.ERR, "FOR statement failed to execute.");
							}
							else
							{
								e.SendMessage(Engine.Level.INF, "FOR self regulated loop break");
								e.returnedValue = forEngine.returnedValue = forEngine.returnedValue[(forEngine.returnedValue.IndexOf('&') + 1)..];
							}
							e.StatementJumpOut("ENDF", "FOR");
						}
					}
					break;

				case "WHILE":   // WHILE [script] [compare instr] [val 1] [val 2]	r-r-r-r
								// WHILE [compare instr] [val 1] [val 2]			r-r-r		(+ENDW)
					if (args.Length > 3)
					{
						Engine whileLoopEngine;

						string[] compareIn = new string[3];
						compareIn[0] = args[1];
						bool selfRegulatedBreak = false;
						bool skipNext = false;

						while (InternalCompare(ref compareIn, ref lineInfo, ref e))
						{
							whileLoopEngine = new Engine(e.scriptFile, args[0]);
							if (whileLoopEngine.Executable())
							{
								if (!skipNext)
								{
									e.returnedValue = whileLoopEngine.returnedValue = whileLoopEngine.StartExecution();
								}
								else
								{
									skipNext = false;
								}
								if (whileLoopEngine.returnedValue.StartsWith("3&"))
								{
									e.SendMessage(Engine.Level.INF, "Break statement");
									selfRegulatedBreak = true;
									break;
								}
								else if (whileLoopEngine.returnedValue.StartsWith("4&"))
								{
									e.SendMessage(Engine.Level.INF, "Continue statement");
									skipNext = true;
									continue;
								}
							}
							else
							{
								if (!selfRegulatedBreak)
								{
									e.SendMessage(Engine.Level.ERR, "WHILE statement failed to execute.");
								}
								else
								{
									e.SendMessage(Engine.Level.INF, "WHILE self regulated loop break");
									e.returnedValue = whileLoopEngine.returnedValue = whileLoopEngine.returnedValue[(whileLoopEngine.returnedValue.IndexOf('&') + 1)..];
								}
								e.StatementJumpOut("ENDW", "WHILE");
							}
						}
						break;

					}
					else
					{
						Engine whileEngine = new Engine(e.scriptFile)
						{
							localMemory = e.localMemory,            // Copy over local memory, and return
							returnedValue = e.returnedValue
						};

						string[] compareIn = new string[3];
						compareIn[0] = args[0];
						bool selfRegulatedBreak = false;
						{
							int i = 0;
							while (InternalCompare(ref compareIn, ref lineInfo, ref e))
							{
								if (i != 0)
								{
									if (whileEngine.returnedValue.Contains('.'))
									{
										whileEngine.returnedValue = whileEngine.returnedValue[(whileEngine.returnedValue.IndexOf('.') + 1)..];
									}
									else
									{
										if (whileEngine.returnedValue.StartsWith("3&"))
										{
											e.SendMessage(Engine.Level.INF, "Break statement");
											selfRegulatedBreak = true;
											break;
										}

										else if (whileEngine.returnedValue.StartsWith("4&"))
										{
											e.SendMessage(Engine.Level.INF, "Continue statement");
											whileEngine.returnedValue = whileEngine.StartExecution(true, e.lineIndex);
											continue;
										}

										else
										{
											e.SendMessage(Engine.Level.INF, "Return statement");
											string returned = whileEngine.returnedValue;
											return returned;
										}
									}
								}
								else
								{
									i++;
								}
								whileEngine.returnedValue = whileEngine.StartExecution(true, e.lineIndex);
							}
						}
						e.localMemory = whileEngine.localMemory; // Copy back
						if (whileEngine.returnedValue.Contains('.'))
						{
							int jumpLine = Parse<int>(whileEngine.returnedValue[..whileEngine.returnedValue.IndexOf('.')]);
							e.returnedValue = whileEngine.returnedValue[(whileEngine.returnedValue.IndexOf('.') + 1)..];
							e.JumpToLine(ref e.sr, ref e.line, ref e.lineIndex, ref jumpLine);
						}
						else
						{
							if (!selfRegulatedBreak)
							{
								e.SendMessage(Engine.Level.ERR, "WHILE statement failed to execute.");
							}
							else
							{
								e.SendMessage(Engine.Level.INF, "WHILE self regulated loop break");
								e.returnedValue = whileEngine.returnedValue = whileEngine.returnedValue[(whileEngine.returnedValue.IndexOf('&') + 1)..];
							}
							e.StatementJumpOut("ENDW", "WHILE");
						}
					}
					break;

				case "DOWHILE": // DOWHILE [script] [compare instr] [val 1] [val 2]		r-r-r-r
								// DOWHILE [compare instr] [val 1] [val 2]				r-r-r		(+ENDDW)
					if (args.Length > 3)
					{
						Engine whileLoopEngine;

						string[] compareIn = new string[3];
						compareIn[0] = args[1];
						bool selfRegulatedBreak = false;
						bool skipNext = false;

						do
						{
							whileLoopEngine = new Engine(e.scriptFile, args[0]);
							if (whileLoopEngine.Executable())
							{
								if (!skipNext)
								{
									e.returnedValue = whileLoopEngine.returnedValue = whileLoopEngine.StartExecution();
								}
								else
								{
									skipNext = false;
								}
								if (whileLoopEngine.returnedValue.StartsWith("3&"))
								{
									e.SendMessage(Engine.Level.INF, "Break statement");
									selfRegulatedBreak = true;
									break;
								}
								else if (whileLoopEngine.returnedValue.StartsWith("4&"))
								{
									e.SendMessage(Engine.Level.INF, "Continue statement");
									continue;
								}
							}
							else
							{
								if (!selfRegulatedBreak)
								{
									e.SendMessage(Engine.Level.ERR, "WHILE statement failed to execute.");
								}
								else
								{
									e.SendMessage(Engine.Level.INF, "WHILE self regulated loop break");
									e.returnedValue = whileLoopEngine.returnedValue = whileLoopEngine.returnedValue[(whileLoopEngine.returnedValue.IndexOf('&') + 1)..];
								}
								e.StatementJumpOut("ENDDW", "DOWHILE");
							}
						}
						while (InternalCompare(ref compareIn, ref lineInfo, ref e));
					}
					else
					{
						Engine whileEngine = new Engine(e.scriptFile)
						{
							localMemory = e.localMemory,            // Copy over local memory, and return
							returnedValue = e.returnedValue
						};

						string[] compareIn = new string[3];
						compareIn[0] = args[0];
						bool selfRegulatedBreak = false;

						int i = 0;
						do
						{
							if (i != 0)
							{
								if (whileEngine.returnedValue.Contains('.'))
								{
									whileEngine.returnedValue = whileEngine.returnedValue[(whileEngine.returnedValue.IndexOf('.') + 1)..];
								}
								else
								{
									if (whileEngine.returnedValue.StartsWith('3') && whileEngine.returnedValue[1] == '&')
									{
										e.SendMessage(Engine.Level.INF, "Break statement");
										selfRegulatedBreak = true;
										break;
									}

									if (whileEngine.returnedValue == "4" && whileEngine.returnedValue[1] == '&')
									{
										e.SendMessage(Engine.Level.INF, "Continue statement");
										whileEngine.returnedValue = whileEngine.StartExecution(true, e.lineIndex);
										continue;
									}

									e.SendMessage(Engine.Level.INF, "Return statement");
									string returned = whileEngine.returnedValue;
									return returned;
								}
							}
							else
							{
								i++;
							}

							whileEngine.returnedValue = whileEngine.StartExecution(true, e.lineIndex);
						}
						while (InternalCompare(ref compareIn, ref lineInfo, ref e));
						e.localMemory = whileEngine.localMemory; // Copy back
						if (whileEngine.returnedValue.Contains('.'))
						{
							int jumpLine = Parse<int>(whileEngine.returnedValue[..whileEngine.returnedValue.IndexOf('.')]);
							e.returnedValue = whileEngine.returnedValue[(whileEngine.returnedValue.IndexOf('.') + 1)..];
							e.JumpToLine(ref e.sr, ref e.line, ref e.lineIndex, ref jumpLine);
						}
						else
						{
							if (!selfRegulatedBreak)
							{
								e.SendMessage(Engine.Level.ERR, "DOWHILE statement failed to execute.");
							}
							else
							{
								e.SendMessage(Engine.Level.INF, "DOWHILE self regulated loop break");
								e.returnedValue = whileEngine.returnedValue = whileEngine.returnedValue[(whileEngine.returnedValue.IndexOf('&') + 1)..];
							}
						}
					}
					break;

				case "SLEEP": // SLEEP [time] r
					{
						int mseconds = Parse<int>(args[0]);
						try
						{
							Thread.Sleep(mseconds);
						}
						catch (ThreadInterruptedException)
						{
							e.SendMessage(Engine.Level.WRN, "SLEEP interrupted by an internal thread.");
						}
					}
					break;

				case "CASEU": // CASEU [variable] [input] r-o
					{
						string output;
						if (args.Length > 1)
						{
							output = args[1];
						}
						else
						{
							output = '$' + args[0];
							e.LocalMemoryGet(ref output);
						}
						output = output.ToUpper();
						e.SetVariable(args[0], ref output);
					}
					break;

				case "CASEL": // CASEL [variable] [input] r-o
					{
						string output;
						if (args.Length > 1)
						{
							output = args[1];
						}
						else
						{
							output = '$' + args[0];
							e.LocalMemoryGet(ref output);
						}
						output = output.ToLower();
						e.SetVariable(args[0], ref output);
					}
					break;

				case "BREAK":
					return "3&" + e.returnedValue;

				case "CONTINUE":
					return "4&" + e.returnedValue;

				case "NOP": // NO OPERATION
					break;

				case "TOBIN": // TOBIN [variable] [integer] r-o
					{
						int value;
						if (args.Length > 1)
						{
							value = Parse<int>(args[1]);
						}
						else
						{
							string varName = '$' + args[0];
							e.LocalMemoryGet(ref varName);
							value = Parse<int>(varName);
						}
						string binary = Convert.ToString(value, 2);
						e.SetVariable(args[0], ref binary);
					}
					break;

				case "BINTOINT": // BINTOINT [variable] [integer] r-o
					{
						string value;
						if (args.Length > 1)
						{
							value = args[1];
						}
						else
						{
							value = '$' + args[0];
							e.LocalMemoryGet(ref value);
						}
						value = Convert.ToInt32(value, 2).ToString();
						e.SetVariable(args[0], ref value);
					}
					break;

				case "CONMODE": // CONMODE [hide|show] r
					if (args[0].ToUpper() == "HIDE") // 0: SW_HIDE 5: SW_SHOW
					{
						Program.ShowWindow(Program.GetConsoleWindow(), 0);
					}
					else
					{
						Program.ShowWindow(Program.GetConsoleWindow(), 5);
					}
					break;

				default:
					recognizedInstruction = false;
					return null;

			}
			recognizedInstruction = true;
			return null;
		}
	}
}
