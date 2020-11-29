﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

#pragma warning disable IDE0044 // Add readonly modifier

namespace Maartanic
{
	class Engine
	{
		private StreamReader sr;
		private int logLevel;

		private bool executable;
		private string scriptFile;
		private string entryPoint = "main";

		private string line;
		private int lineIndex;
		private string[] lineInfo;

		private bool compareOutput = false;
		private bool keyOutput = false;
		private string returnedValue = "NULL";

		private Mode applicationMode = Mode.VSB; //TODO: Extend VSB instructions! It's C#, GO FOR IT!
		private DateTime startTime = DateTime.UtcNow;

		private Dictionary<string, Delegate> predefinedVariables = new Dictionary<string, Delegate>();
		private Dictionary<string, string> localMemory = new Dictionary<string, string>();

		/* Level: Used in SendMessage method to indicate the message level as info, warning or error. */
		private enum Level
		{
			INF,
			WRN,
			ERR
		}

		/* Mode: Used in applicationMode to let the engine know to enable extended MRT functions not yet included in the VSB Engine. */
		private enum Mode
		{
			MRT,
			VSB
		}

		/* FillPredefinedList(): Fills the predefinedVariables array with Delegates (Functions) to accommodate for the system in VSB */
		private void FillPredefinedList()
		{ //TODO console cursorleft cursortop methods for mouse pos? ($_mx, $_my)
			predefinedVariables.Add("ww", (Func<string>)(() => Console.WindowWidth.ToString()));
			predefinedVariables.Add("wh", (Func<string>)(() => Console.WindowHeight.ToString()));
			predefinedVariables.Add("cmpr", (Func<string>)(() => compareOutput.ToString()));
			predefinedVariables.Add("projtime", (Func<string>)(() => (DateTime.UtcNow - startTime).TotalSeconds.ToString()));
			predefinedVariables.Add("projid", (Func<string>)(() => "0"));
			predefinedVariables.Add("user", (Func<string>)(() => "*guest"));
			predefinedVariables.Add("ver", (Func<string>)(() => "1.3"));
			predefinedVariables.Add("ask", (Func<string>)(() => Console.ReadLine()));
			predefinedVariables.Add("graphics", (Func<string>)(() => "false"));
			predefinedVariables.Add("thour", (Func<string>)(() => DateTime.UtcNow.Hour.ToString()));
			predefinedVariables.Add("tminute", (Func<string>)(() => DateTime.UtcNow.Minute.ToString()));
			predefinedVariables.Add("tsecond", (Func<string>)(() => DateTime.UtcNow.Second.ToString()));
			predefinedVariables.Add("tyear", (Func<string>)(() => DateTime.UtcNow.Year.ToString()));
			predefinedVariables.Add("tmonth", (Func<string>)(() => DateTime.UtcNow.Month.ToString()));
			predefinedVariables.Add("tdate", (Func<string>)(() => DateTime.UtcNow.Day.ToString()));
			predefinedVariables.Add("tdow", (Func<string>)(() => ((int)DateTime.UtcNow.DayOfWeek).ToString()));
			predefinedVariables.Add("key", (Func<string>)(() => keyOutput.ToString()));
			predefinedVariables.Add("ret", (Func<string>)(() => returnedValue));
		}

		/* Engine(): Class constructor, returns if given file does not exist. */
		public Engine(string startPos)
		{
			executable = File.Exists(startPos);
			if (!executable)
			{
				Console.WriteLine($"The file {startPos} does not exist.");
				return;
			}
			FillPredefinedList();
			scriptFile = startPos;
		}

		/* Engine() OVERLOADED: Specify your entry point */
		public Engine(string startPos, string customEntryPoint)
		{
			entryPoint = customEntryPoint; // default is main
			executable = File.Exists(startPos);
			if (!executable)
			{
				Console.WriteLine($"The file {startPos} does not exist.");
				return;
			}
			FillPredefinedList();
			scriptFile = startPos;
		}

		/* Executable(): Returns whether or not it is ready to be executed based on Engine()'s result. */
		public bool Executable()
		{
			return executable;
		}

		/* FindProgram(): Basically -jumps- to a method declaration in code */
		private bool FindProgram(ref StreamReader sr, ref string line, ref int lineIndex)
		{
			while (((line = sr.ReadLine()) != null) && line != $"DEF {entryPoint}")
			{
				lineIndex++;
			}
			if (line == $"DEF {entryPoint}")
			{
				lineIndex++;
				return true;
			}
			else
				return false; // No entry point "main"!
		}

		/* SendMessage(): Logs a message to the console with a level, including line of execution. */
		private void SendMessage(Level a, string message)
		{
			if ((int) a >= logLevel)
			{
				switch ((int) a)
				{
					case 0:
						Console.Write($"\nMRT INF line {lineIndex}: {message}");
						break;
					case 1:
						Console.Write($"\nMRT WRN line {lineIndex}: {message}");
						break;
					case 2:
						Console.Write($"\nMRT ERR line {lineIndex}: {message}");
						break;
				}
			}	
		}

		/* LineCheck(): Splits the text into an array for further operations. */
		public bool LineCheck(ref string[] lineInfo, ref int lineIndex)
		{
			if (line == null)
			{
				SendMessage(Level.ERR, "Unexpected NULL");
				line = "";
			}
			lineInfo = line.Trim().Split(' ');

			// Check if empty
			if (lineInfo.Length > 0)
			{
				if (lineInfo[0].Length > 0)
				{
					char x = lineInfo[0][0];
					if (x == ';') // A wild comment appeared!
					{
						lineIndex++;
						return true;
					}
					if (x == '[')
					{
						ExtractEngineArgs(ref lineInfo);
						return true;
					}
				}
			}
			else
			{
				lineIndex++;
				return true;
			}
			return false;
		}

		/* StartExecution(): "Entry point" to the program. This goes line by line, and executes instructions. */
		public string StartExecution(int logLevelIN)
		{
			logLevel = logLevelIN;
			lineIndex = 0;
			sr = new StreamReader(scriptFile);
			if (!FindProgram(ref sr, ref line, ref lineIndex))
			{
				// unknown error
				Console.WriteLine("Unknown error");
			}
			while ((line = sr.ReadLine()) != null)
			{
				lineIndex++;

				if (LineCheck(ref lineInfo, ref lineIndex))
				{
					continue;
				}

				string[] args = ExtractArgs(ref lineInfo);
				switch (lineInfo[0].ToUpper())
				{

					case "PRINT":
						if (lineInfo.Length == 1)
						{
							SendMessage(Level.WRN, "No arguments given to print.");
							break;
						}
						Console.Write('\n');
						foreach (string arg in args)
						{
							Console.Write(arg);
						}
						break;

					case "OUT":
						if (lineInfo.Length == 1)
						{
							SendMessage(Level.WRN, "No arguments given to OUT. OUT does absolutely nothing without an argument.");
							break;
						}
						foreach (string arg in ExtractArgs(ref lineInfo))
						{
							Console.Write(arg);
						}
						break;

					case "ENDDEF": // Possible end-of-function
						if (lineInfo[1] == entryPoint)
						{
							SendMessage(Level.INF, "End of definition");
							return "NULL";
						}
						else
						{
							SendMessage(Level.ERR, "Unexpected end of definition, expect unwanted side effects.");
						}
						break;

					case "CLEAR":
						if (args.Length != 0)
						{
							if (!Int32.TryParse(args[0], out int imax))
							{
								imax = 0;
								SendMessage(Level.ERR, "Malformed number found.");
							}
							for (int i = 0; i < imax; i++)
							{
								Console.SetCursorPosition(0, Console.CursorTop);
								Console.Write(new String(' ', Console.BufferWidth));
								Console.SetCursorPosition(0, Console.CursorTop - 1);
								if (i == imax - 1)
								{
									Console.SetCursorPosition(0, Console.CursorTop - 1);
								}
							}
							Console.SetCursorPosition(0, Console.CursorTop + 1);
						}
						else
						{
							Console.Clear();
						}
						break;

					case "NEW":
						if (predefinedVariables.ContainsKey(args[0][1..]))
						{
							SendMessage(Level.ERR, $"Variable {args[0]} is a predefined variable and cannot be declared.");
							break;
						}
						if (localMemory.ContainsKey(args[0]))
						{
							SendMessage(Level.WRN, $"Variable {args[0]} already exists.");
							localMemory[args[0]] = args.Length > 1 ? args[1] : "0";
						}
						else
						{
							localMemory.Add(args[0], args.Length > 1 ? args[1] : "0");
						}
						break;

					case "IF":
						{ // local scope to make variables defined here local to this scope!
							string statement = args.Length > 1 ? args[1] : args[0];
							bool result;
							bool invertStatement = args.Length > 1 && args[0].ToUpper() == "NOT";
							if (statement == "1" || statement.ToUpper() == "TRUE")
							{
								result = true;
							}
							else
							{
								result = localMemory.ContainsKey(statement);
							}
							result = invertStatement ? !result : result;
							if (!result)
							{
								int scope = 0;
								bool success = false;
								int ifLineIndex = 0;
								string[] cLineInfo = null;
								StreamReader ifsr = new StreamReader(scriptFile);
								while ((line = ifsr.ReadLine()) != null)
								{
									ifLineIndex++;
									if (LineCheck(ref cLineInfo, ref ifLineIndex))
									{
										continue;
									}
									if (ifLineIndex > lineIndex)
									{										
										if ((cLineInfo[0].ToUpper() == "ELSE" || cLineInfo[0].ToUpper() == "ENDIF") && scope == 0)
										{
											success = true;
											break;
										}
										if (cLineInfo[0].ToUpper() == "IF")
										{
											scope++;
										}
										if (cLineInfo[0].ToUpper() == "ENDIF")
										{
											scope--;
										}
									}
								}
								if (success)
								{
									for (int i = lineIndex; i < ifLineIndex; i++)
									{
										if ((line = sr.ReadLine()) == null)
										{
											break; // safety protection?
										}
									}
									lineIndex = ifLineIndex;
								}
								else
								{
									SendMessage(Level.ERR, "Could not find a spot to jump to.");
								}
							}
						}
						break;

					case "ENDIF": // To be ignored
						break;

					case "ELSE":
						{
							int scope = 0;
							bool success = false;
							int endifLineIndex = 0;
							string[] cLineInfo = null;
							StreamReader endifsr = new StreamReader(scriptFile);
							while ((line = endifsr.ReadLine()) != null)
							{
								endifLineIndex++;
								if (LineCheck(ref cLineInfo, ref endifLineIndex))
								{
									continue;
								}
								if (endifLineIndex > lineIndex)
								{
									if (cLineInfo[0].ToUpper() == "ENDIF" && scope == 0)
									{
										success = true;
										break;
									}
									if (cLineInfo[0].ToUpper() == "IF")
									{
										scope++;
									}
									if (cLineInfo[0].ToUpper() == "ENDIF")
									{
										scope--;
									}
								}
								
							}
							if (success)
							{
								for (int i = lineIndex; i < endifLineIndex; i++)
								{
									if ((line = sr.ReadLine()) == null)
									{
										break; // safety protection?
									}
								}
								lineIndex = endifLineIndex;
							}
							else
							{
								SendMessage(Level.ERR, "Could not find a spot to jump to.");
							}
						}
						break;

					case "SET":
						if (localMemory.ContainsKey(args[0]))
						{
							localMemory[args[0]] = args[1];
						}
						else
						{
							SendMessage(Level.ERR, $"The variable {args[0]} does not exist.");
						}
						break;

					case "DEL":
						if (localMemory.ContainsKey(args[0]))
						{
							localMemory.Remove(args[0]);
						}
						else
						{
							SendMessage(Level.WRN, $"Tried removing a non-existing variable called {args[0]}.");
						}
						break;

					case "ADD": // Pass arg[2] if it exists else ignore it
						{
							string tmp = Convert.ToString(MathOperation('+', args[0], args[1], args.Length > 2 ? args[2] : null));
							SetVariable(args[0], ref tmp);
						}
						break;

					case "SUB":
						{
							string tmp = Convert.ToString(MathOperation('-', args[0], args[1], args.Length > 2 ? args[2] : null));
							SetVariable(args[0], ref tmp);
						}
						break;

					case "DIV":
						{
							string tmp = Convert.ToString(MathOperation('/', args[0], args[1], args.Length > 2 ? args[2] : null));
							SetVariable(args[0], ref tmp);
						}
						break;

					case "MUL":
						{
							string tmp = Convert.ToString(MathOperation('*', args[0], args[1], args.Length > 2 ? args[2] : null));
							SetVariable(args[0], ref tmp);
						}
						break;

					case "CMPR":
						{
							Compare(ref args);
						}
						break;

					case "ROUND":
						{
							string varName, num1IN, sizeIN;
							if (args.Length > 2)
							{
								varName = args[0];
								num1IN = args[1];
								sizeIN = args[2];
							}
							else
							{
								varName = args[0];
								num1IN = '$' + varName;
								LocalMemoryGet(ref num1IN);
								sizeIN = args[1];
							}
							if (!Decimal.TryParse(num1IN, out decimal num1))
							{
								num1 = 0.0M;
								SendMessage(Level.ERR, "Malformed number found.");
							}
							if (!Int32.TryParse(sizeIN, out int size))
							{
								size = 0;
								SendMessage(Level.ERR, "Malformed number found.");
							}
							string output = Math.Round(num1, size).ToString();
							SetVariable(args[0], ref output);
						}
						break;

					case "COLRGBTOHEX":
						{
							string varName = args[0];
							if (!Int32.TryParse(args[1], out int r)) { r = 0; SendMessage(Level.ERR, "Malformed number found."); }
							if (!Int32.TryParse(args[2], out int g)) { g = 0; SendMessage(Level.ERR, "Malformed number found."); }
							if (!Int32.TryParse(args[3], out int b)) { b = 0; SendMessage(Level.ERR, "Malformed number found."); }
							string output = $"{r:X2}{g:X2}{b:X2}";
							SetVariable(varName, ref output);
						}
						break;

					case "RAND":
						{
							string varName = args[0];
							if (!Int32.TryParse(args[1], out int lowerLim)) { lowerLim = 0; SendMessage(Level.ERR, "Malformed number found."); }
							if (!Int32.TryParse(args[2], out int higherLim)) { higherLim = 0; SendMessage(Level.ERR, "Malformed number found."); }
							Random generator = new Random();
							string output = generator.Next(lowerLim, higherLim + 1).ToString();
							SetVariable(varName, ref output);
						}
						break;

					case "SIZE":
						{
							string varName = args[0], output;

							if (args.Length > 1)
							{
								output = args[1].Length.ToString();
							}
							else
							{
								output = '$' + varName;
								LocalMemoryGet(ref output);
								output = output.Length.ToString();
							}
							SetVariable(varName, ref output);
						}
						break;

					case "ABS":
						{
							string varName = args[0], output;
							decimal n;
							if (args.Length > 1)
							{
								if (!Decimal.TryParse(args[1], out n)) { n = 0.0M; SendMessage(Level.ERR, "Malformed number found."); }
								output = Math.Abs(n).ToString();
							}
							else
							{
								output = '$' + varName;
								LocalMemoryGet(ref output);
								if (!Decimal.TryParse(output, out n)) { n = 0.0M; SendMessage(Level.ERR, "Malformed number found."); }
								output = Math.Abs(n).ToString();
							}
							SetVariable(varName, ref output);
						}
						break;

					case "MIN":
						PerformOp("min", args[0], args[1], args.Length > 2 ? args[2] : null);
						break;
					case "MAX":
						PerformOp("max", args[0], args[1], args.Length > 2 ? args[2] : null);
						break;
					case "CON":
						{
							string a, b, output;
							if (args.Length > 2)
							{
								a = args[1];
								b = args[2];
							}
							else
							{
								a = '$' + args[0];
								LocalMemoryGet(ref a);
								b = args[1];
							}
							output = a + b;
							SetVariable(args[0], ref output);
						}
						break;

					case "KEY":
						{
							if (!Char.TryParse(args[0], out char key)) { key = 'x'; SendMessage(Level.ERR, "Malformed character found."); }
							ConsoleKeyInfo cki;
							if (Console.KeyAvailable)
							{
								cki = Console.ReadKey();
								keyOutput = cki.KeyChar == key;
							}	
						}
						break;

					case "HLT":
						SendMessage(Level.INF, "HLT");
						Program.Exit("NULL");
						break; // Unreachable code but IDE complains for some reason

					case "SUBSTR":
						{
							string input, output;
							int start, len;
							if (args.Length > 3)
							{
								input = args[1];
								if (!Int32.TryParse(args[2], out start)) { start = 0; SendMessage(Level.ERR, "Malformed number found."); }
								if (!Int32.TryParse(args[3], out len)) { len = 0; SendMessage(Level.ERR, "Malformed number found."); }
							}
							else
							{
								input = '$' + args[0];
								LocalMemoryGet(ref input);
								if (!Int32.TryParse(args[1], out start)) { start = 0; SendMessage(Level.ERR, "Malformed number found."); }
								if (!Int32.TryParse(args[2], out len)) { len = 0; SendMessage(Level.ERR, "Malformed number found."); }
							}
							output = input.Substring(start, len);
							SetVariable(args[0], ref output);
						}
						break;

					case "CHARAT":
						{
							string input, output = "NULL";
							int index;
							if (args.Length > 2)
							{
								input = args[1];
								if (!Int32.TryParse(args[2], out index)) { index = -1; SendMessage(Level.ERR, "Malformed number found."); }
							}
							else
							{
								input = '$' + args[0];
								LocalMemoryGet(ref input);
								if (!Int32.TryParse(args[1], out index)) { index = -1; SendMessage(Level.ERR, "Malformed number found."); }
							}
							if (index < 0 || index >= input.Length)
							{
								SendMessage(Level.ERR, $"Index {index} is out of bounds.");
							}
							else
							{
								output = input[index].ToString();
							}
							SetVariable(args[0], ref output);
						}
						break;

					case "TRIM":
						{
							string input;
							if (args.Length > 1)
							{
								input = args[1];
							}
							else
							{
								input = '$' + args[0];
								LocalMemoryGet(ref input);
							}
							input = System.Text.RegularExpressions.Regex.Replace(input.Trim(), @"\s+", " "); // Trim and remove duplicate spaces
							SetVariable(args[0], ref input);
						}
						break;

					case "DO":
					case "CALL":
						{
							Engine E = new Engine(scriptFile, args[0]);
							if (E.Executable())
							{
								returnedValue = E.StartExecution(logLevel);
							}
							else
							{
								SendMessage(Level.ERR, "Program was not executable");
							}
						}
						break;

					case "RET":
						if (args.Length > 0)
						{
							return args[0];
						}
						return "NULL";

					case "RPLC":
						{
							string output, input, old, _new; // "new" is a C# keyword so use "_new" instead!
							if (args.Length > 3)
							{
								input = args[1];
								old = args[2];
								_new = args[3];
							}
							else
							{
								input = '$' + args[0];
								LocalMemoryGet(ref input);
								old = args[1];
								_new = args[2];
							}
							output = input.Replace(old, _new);
							SetVariable(args[0], ref output);
						}
						break;

					case "COUNT":
						{
							string output, input, value;
							if (args.Length > 2)
							{
								input = args[1];
								value = args[2];
							}
							else
							{
								input = '$' + args[0];
								LocalMemoryGet(ref input);
								value = args[1];
							}
							output = Regex.Matches(input, value).Count.ToString();
							SetVariable(args[0], ref output);
						}
						break;

					case "FIND":
						{
							string output, input, value;
							if (args.Length > 2)
							{
								input = args[1];
								value = args[2];
							}
							else
							{
								input = '$' + args[0];
								LocalMemoryGet(ref input);
								value = args[1];
							}
							output = input.IndexOf(value).ToString();
							SetVariable(args[0], ref output);
						}
						break;

					case "COS": // (Trigonometric) math functions
					case "SIN":
					case "TAN":
					case "ACOS":
					case "ASIN":
					case "ATAN":
					case "LOG":
					case "MATH_LN":
					case "EPOW":
					case "TENPOW":
					case "TORAD":
					case "TODEG":
					case "FLR":
					case "CEIL":
						MathFunction(lineInfo[0].ToUpper(), args[0], args.Length > 1 ? args[1] : null);
						break;

					case "PUSH":
						Program.stack.Push(args[0]);
						break;

					case "POP":
						{
							string output;
							if (Program.stack.HasNext())
							{
								Program.stack.Pop(out output);
							}
							else
							{
								output = "NULL";
								SendMessage(Level.ERR, "Stack was empty and could not be popped from.");
							}
							SetVariable(args[0], ref output);
						}
						break;

					case "QPUSH":
						Program.queue.Enqueue(args[0]);
						break;

					case "QPOP":
						{
							string output;
							if (Program.queue.HasNext())
							{
								Program.queue.Dequeue(out output);
							}
							else
							{
								output = "NULL";
								SendMessage(Level.ERR, "Queue was empty and could not be dequeued from.");
							}
							SetVariable(args[0], ref output);
						}
						break;

					case "ALOC":
						{
							if (!Int32.TryParse(args[0], out int amount)) { amount = 0; SendMessage(Level.ERR, "Malformed number found."); }
							for (int i = 0; i < amount; i++)
							{
								Program.memory.Add("0");
							}
						}
						break;

					case "FREE":
						{
							if (!Int32.TryParse(args[0], out int amount)) { amount = 0; SendMessage(Level.ERR, "Malformed number found."); }
							for (int i = 0; i < amount; i++)
							{
								if (!Program.memory.Exists(0))
								{
									SendMessage(Level.WRN, "Tried freeing memory that doesn't exist.");
									continue;
								}
								else
								{
									Program.memory.Remove(1);
								}
							}
						}
						break;

					case "SETM":
						{
							if (!Int32.TryParse(args[0], out int address)) { SendMessage(Level.ERR, "Malformed number found."); break; }
							SetMemoryAddr(address, args[1]);
						}
						break;

					case "GETM":
						{
							if (!Int32.TryParse(args[1], out int address)) { SendMessage(Level.ERR, "Malformed number found."); break;  }
							Program.memory.Get(address, out string output);
							SetVariable(args[0], ref output);
						}
						break;

					default:
						SendMessage(Level.ERR, $"Instruction {lineInfo[0]} is not recognized.");
						break;
				}
			}
			sr.Close(); // Close StreamReader after execution
			return "NULL";
		}

		/* SetMemoryAddr(): Sets a given memory address to the given value.  */
		private void SetMemoryAddr(int address, string value)
		{
			if (!Program.memory.Exists(address))
			{
				SendMessage(Level.ERR, $"Memory address {address} does not exist.");
			}
			else
			{
				Program.memory.Set(address, value);
			}
		}

		/* ToRadians(): Converts a given angle in degrees to radians with a limited amount of accuracy */
		private double ToRadians(double input)
		{
			return 0.01745329251 * input;
		}

		/* ToDegrees(): Converts a given angle in radians to degrees with a limited amount of accuracy */
		private double ToDegrees(double input)
		{
			return 57.2957795131 * input;
		}

		/* MathFunction(): Method merges multiple cases in the big switch of StartExecution(). */
		private void MathFunction(string function, string destination, string number)
		{
			double dnumA;
			if (number == null)
			{
				string tmp = '$' + destination;
				LocalMemoryGet(ref tmp);
				if (!Double.TryParse(tmp, out dnumA)) { dnumA = 0.0d; SendMessage(Level.ERR, "Malformed number found."); }
			}
			else
			{
				if (!Double.TryParse(number, out dnumA)) { dnumA = 0.0d; SendMessage(Level.ERR, "Malformed number found."); }
			}
			double result;
			switch (function)
			{
				case "COS": // To radians, use it, and back to degrees.
					result = Math.Cos(ToRadians(dnumA));
					break;

				case "SIN":
					result = Math.Sin(ToRadians(dnumA));
					break;

				case "TAN":
					result = Math.Tan(ToRadians(dnumA));
					break;

				case "ACOS":
					result = Math.Acos(ToRadians(dnumA));
					break;

				case "ASIN":
					result = Math.Asin(ToRadians(dnumA));
					break;

				case "ATAN":
					result = Math.Atan(ToRadians(dnumA));
					break;

				case "LOG": // Log w/ base 10
					result = Math.Log10(dnumA);
					break;

				case "MATH_LN": // Natural logarithm (e as base)
					result = Math.Log(dnumA);
					break;

				case "EPOW":
					result = Math.Exp(dnumA);
					break;

				case "TENPOW":
					result = Math.Pow(10, dnumA);
					break;

				case "TORAD":
					result = ToRadians(dnumA);
					break;

				case "TODEG":
					result = ToDegrees(dnumA);
					break;

				case "FLR":
					result = Math.Floor(dnumA);
					break;

				case "CEIL":
					result = Math.Ceiling(dnumA);
					break;

				default:
					result = 0.0d;
					SendMessage(Level.ERR, $"Unrecognized function {function}.");
					break;
			}
			string resultS = result.ToString();
			SetVariable(destination, ref resultS);
		}

		/* PerformOp(): Performs an operation with two values given. */
		private void PerformOp(string operation, string varName, string num1, string num2)
		{
			double numberA, numberB;
			if (num2 == null)
			{
				string num1_var = '$' + varName;
				LocalMemoryGet(ref num1_var);
				if (!Double.TryParse(num1_var, out numberA)) { numberA = 0.0d; SendMessage(Level.ERR, "Malformed number found."); }
				if (!Double.TryParse(num1, out numberB)) { numberB = 0.0d; SendMessage(Level.ERR, "Malformed number found."); }
			}
			else
			{
				if (!Double.TryParse(num1, out numberA)) { numberA = 0.0d; SendMessage(Level.ERR, "Malformed number found."); }
				if (!Double.TryParse(num2, out numberB)) { numberB = 0.0d; SendMessage(Level.ERR, "Malformed number found."); }
			}
			string result = "";
			switch (operation)
			{
				case "min":
					result = Math.Min(numberA, numberB).ToString();
					break;

				case "max":
					result = Math.Max(numberA, numberB).ToString();
					break;

				default:
					SendMessage(Level.ERR, $"Unrecognized operation {operation}.");
					break;
			}
			SetVariable(varName, ref result);
		}

		/* Compares two values inside the args array, and stores the result in compareOutput. */
		private void Compare(ref string[] args)
		{
			bool r; // Output variable (result)
			bool b1, b2;
			// Numbers
			b1 = args[1] == "true" || args[1] == "1";
			b2 = args[2] == "true" || args[2] == "1";
			if (!Double.TryParse(args[1], out double n1))
			{
				n1 = 0.0d;
			}
			if (!Double.TryParse(args[2], out double n2))
			{
				n2 = 0.0d;
			}

			switch (args[0].ToUpper())
			{
				case "EQL":
				case "E":
					r = args[1] == args[2];
					break;

				case "NEQL":
				case "NE":
					r = args[1] != args[2];
					break;

				case "G":
					r = n1 > n2;
					break;

				case "NG":
					r = !(n1 > n2);
					break;

				case "GE":
					r = n1 >= n2;
					break;

				case "NGE":
					r = !(n1 >= n2);
					break;

				case "L":
					r = n1 < n2;
					break;

				case "NL":
					r = !(n1 < n2);
					break;

				case "LE":
					r = n1 <= n2;
					break;

				case "NLE":
					r = !(n1 <= n2);
					break;

				case "OR":
					r = b1 || b2;
					break;

				case "AND":
					r = b1 && b2;
					break;

				case "XOR":
					r = (b1 || b2) && !(b1 && b2);
					break;

				case "XNOR":
					r = !((b1 || b2) && !(b1 && b2));
					break;

				case "NOR":
					r = (b1 == false) && b2 == false;
					break;

				case "NAND":
					r = !(b1 && b2);
					break;

				default:
					r = false;
					SendMessage(Level.ERR, $"Unrecognized CMPR option {args[0].ToUpper()}.");
					break;

			}
			compareOutput = r;
		}

		/* MathOperation(): Calculator */
		private double MathOperation(char op, string destination, string number, string optnumber = null)
		{
			double num1, num2;
			if (optnumber == null)
			{
				string tmp1 = "$" + destination;
				LocalMemoryGet(ref tmp1);
				if (!Double.TryParse(tmp1, out num1))
				{
					num1 = 0.0d;
					SendMessage(Level.ERR, "Malformed number found.");
				}
				if (!Double.TryParse(number, out num2))
				{
					num2 = 0.0d;
					SendMessage(Level.ERR, "Malformed number found.");
				}
			}
			else
			{
				if (!Double.TryParse(number, out num1))
				{
					num1 = 0.0d;
					SendMessage(Level.ERR, "Malformed number found.");
				}
				if (!Double.TryParse(optnumber, out num2))
				{
					num2 = 0.0d;
					SendMessage(Level.ERR, "Malformed number found.");
				}
			}

			switch (op)
			{
				case '+':
					return num1 + num2;
				case '-':
					return num1 - num2;
				case '*':
					return num1 * num2;
				case '/':
					return num1 / num2;
				case '%':
					return num1 % num2;
				default:
					SendMessage(Level.ERR, $"Invalid operator {op} used");
					return 0.0d;
			}
		}

		/* SetVariable(): Sets the variable with the name varName to newData. Lets the user know if it doesn't exist. */
		private void SetVariable(string varName, ref string newData)
		{
			if (localMemory.ContainsKey(varName))
			{
				localMemory[varName] = newData;
			}
			else
			{
				SendMessage(Level.ERR, $"The variable {varName} does not exist.");
			}
		}

		/* LocalMemoryGet(): Converts a given variable to its contents. Leaves it alone if it doesn't have a prefix '$'. */
		private void LocalMemoryGet(ref string varName)
		{
			if (varName.Length == 0)
			{
				varName = "NULL";
				SendMessage(Level.ERR, "Malformed variable");
				return;
			}
			if (varName[0] == '$')
			{
				if (varName[1] == '_')
				{
					if (predefinedVariables.ContainsKey(varName[2..]))
					{
						varName = (string) predefinedVariables[varName[2..]].DynamicInvoke();
					}
				}
				else
				{
					if (localMemory.ContainsKey(varName[1..]))
					{
						varName = localMemory[varName[1..]];
					}
					else
					{
						SendMessage(Level.ERR, $"The variable {varName[1..]} does not exist.");
						varName = "NULL";
					}
				}
			}
		}

		/* ExtractArgs(): Simply extracts the arguments from array lineInfo, treating quote blocks as one. */
		private string[] ExtractArgs(ref string[] lineInfo)
		{
			string combined = "";
			for (int i = 1; i < lineInfo.Length; i++)
			{
				combined += ' ' + lineInfo[i];
			}
			if (combined.Length < 1)
			{
				return null;
			}
			combined = combined[1..]; // Exclude first space

			// Maybe use RegEx but eh lazy. Escape quotation with a backslash. At least I understand it this way
			// Iterates through it, splits spaces. Things in quotes (") are treated like one block even if there are spaces in between.
			List<string> newCombinedList = new List<string>();
			string newCombined = "";
			bool isInQuotes = false;
			for (int i = 0; i < combined.Length; i++)
			{
				if (combined[i] == '"')
				{
					if (isInQuotes)
					{
						if (combined[i - 1] != '\\')
						{
							isInQuotes = false;
							newCombinedList.Add(newCombined);
							newCombined = "";
							continue;
						}
						else
						{
							newCombined = newCombined[..(newCombined.Length - 1)] + '"'; // Exclude the last/escape character AND include quote
							continue;
						}
					}
					else
					{
						if (i == 0 || combined[i - 1] == ' ')
						{
							isInQuotes = true;
							newCombined = "";
							continue;
						}
					}
				}
				if (isInQuotes)
				{
					newCombined += combined[i];
				}
				else
				{
					if (combined[i] == ' ')
					{
						if (combined[i - 1] != '"')
						{
							newCombinedList.Add(newCombined);
							newCombined = "";
						}
						continue;
					}
					newCombined += combined[i];
				}
				if (i == combined.Length - 1)
				{
					newCombinedList.Add(newCombined);
					newCombined = "";
				}
			}

			{ // Make scope
				string tmp;
				for (int i = 0; i < newCombinedList.Count; i++)
				{
					tmp = newCombinedList[i];
					LocalMemoryGet(ref tmp);
					newCombinedList[i] = tmp;
				}
			}

			return newCombinedList.ToArray();
		}

		/* ExtractEngineArgs(): Extracts [A B] like stuff and applies it to internal engine variables. */
		private void ExtractEngineArgs(ref string[] lineInfo)
		{
			string[] engineArgParts;
			string engineArg = "";
			foreach (string part in lineInfo)
			{
				engineArg += ' ' + part;
			}
			engineArgParts = engineArg[1..].Trim('[', ']').Split(' ');
			if (engineArgParts[0].ToLower() == "mode")
			{
				switch (engineArgParts[1].ToUpper())
				{
					case "VSB":
						applicationMode = Mode.VSB;
						SendMessage(Level.INF, "Using compat mode");
						break;

					case "MRT":
						applicationMode = Mode.MRT;
						SendMessage(Level.INF, "Using extended mode");
						break;

					default:
						SendMessage(Level.ERR, "Unrecognized mode entered.");
						break;
				}
			}
		}
	}
}
