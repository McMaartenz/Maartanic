﻿using System;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Maartanic
{
	class Engine
	{
		internal delegate void EventHandler(object sender, EventArgs args);
		internal event EventHandler MinimizeNow = delegate { };
		internal void MinimizeWindow() => MinimizeNow(this, new EventArgs());

		internal StreamReader sr;
		internal Engine childProcess;

		private readonly bool executable;
		internal readonly string scriptFile;
		internal string entryPoint = "main";
		internal string[] arguments;

		internal string line;
		internal int lineIndex;
		internal bool hasInternalAccess = false;
		private string[] lineInfo;

		private bool compareOutput = false;
		internal bool _isType = false;
		private bool _keyOutput = false;

		internal bool KeyOutput
		{
			get
			{
				return _keyOutput;
			}

			set
			{
				GetLatestChild()._keyOutput = value;
			}
		}
		internal bool IsType
		{
			get
			{
				return _isType;
			}

			set
			{
				GetLatestChild()._isType = value;
			}
		}

		internal string returnedValue = "NULL";
		internal bool redraw;

		internal readonly DateTime startTime = DateTime.UtcNow;

		internal static Dictionary<string, Func<Engine, string>> predefinedVariables;
		internal Dictionary<string, string> localMemory = new Dictionary<string, string>();

		// Level: Used in SendMessage method to indicate the message level as info, warning or error.
		internal enum Level
		{
			INF,
			WRN,
			ERR
		}

		// Mode: Used in applicationMode to let the engine know to enable extended functions not (yet) included in the VSB Engine.
		internal enum Mode
		{
			ENABLED,
			DISABLED
		}

		private static T Parse<T>(string input, bool silence = false)
		{
			return Program.Parse<T>(input, silence);
		}

		// FillPredefinedList(): Fills the predefinedVariables array with Delegates (Functions) to accommodate for the system in VSB
		internal void FillPredefinedList()
		{
			predefinedVariables = new Dictionary<string, Func<Engine, string>>()
			{
				{ "ww",         (e) => Console.WindowWidth.ToString() },
				{ "wh",         (e) => Console.WindowHeight.ToString() },
				{ "cmpr",       (e) => e.compareOutput.ToString() },
				{ "cmp",		(e) => e.compareOutput.ToString() },
				{ "projtime",   (e) => (DateTime.UtcNow - startTime).TotalSeconds.ToString() },
				{ "projid",     (e) => "0" },
				{ "user",       (e) => "*guest" },
				{ "ver",        (e) => "1.3" }, // VSB version not Maartanic Engine version
				{ "ask",        (e) => Console.ReadLine() },
				{ "graphics",   (e) => (Program.SettingGraphicsMode == Mode.ENABLED).ToString().ToLower() },
				{ "thour",      (e) => DateTime.UtcNow.Hour.ToString() },
				{ "tminute",    (e) => DateTime.UtcNow.Minute.ToString() },
				{ "tsecond",    (e) => DateTime.UtcNow.Second.ToString() },
				{ "tyear",      (e) => DateTime.UtcNow.Year.ToString() },
				{ "tmonth",     (e) => DateTime.UtcNow.Month.ToString() },
				{ "tdate",      (e) => DateTime.UtcNow.Day.ToString() },
				{ "tdow",       (e) => ((int)DateTime.UtcNow.DayOfWeek).ToString() },
				{ "key",        (e) => e.KeyOutput.ToString() },
				{ "ret",        (e) => e.returnedValue },
				{ "mx",         (e) => OutputForm.app.GetMouseX().ToString() },
				{ "my",         (e) => OutputForm.app.GetMouseY().ToString() },
				{ "md",			(e) => OutputForm.app.GetLMDown().ToString() },
				{ "redraw",     (e) => redraw.ToString() }
			};
		}

		// Engine(): Class constructor, returns if given file does not exist.
		internal Engine (string file)
		{
			redraw = true;
			KeyOutput = false;
			executable = File.Exists(file);
			if (!executable)
			{
				return;
			}
			scriptFile = file;
		}

		// Engine() OVERLOADED: Specify your entry point
		internal Engine(string startPos, string customEntryPoint)
		{
			redraw = true;
			KeyOutput = false;
			entryPoint = customEntryPoint; // default is main
			executable = File.Exists(startPos);
			if (!executable)
			{
				return;
			}
			FillPredefinedList();
			scriptFile = startPos;
		}

		// Executable(): Returns whether or not it is ready to be executed based on Engine()'s result.
		internal bool Executable()
		{
			return executable;
		}

		// FindProgram(): Basically -jumps- to a method declaration in code
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

		// SendMessage(): Logs a message to the console with a level, including line of execution.
		internal void SendMessage(Level a, string message, uint code = 0)
		{
			if (childProcess != null && childProcess.Executable())
			{
				childProcess.SendMessage(a, message, code);
			}
			else
			{
				if ((int)a >= Program.logLevel)
				{
					switch ((int)a)
					{
						case 0:
							Console.Write($"\nINF l{lineIndex}: {message}");
							break;
						case 1:
							Console.Write($"\nWRN l{lineIndex}: {message}");
							break;
						case 2:
							if (Program.SettingExtendedMode != Mode.ENABLED || !Program.extendedMode.CatchEvent(this, code))
							{
								Console.Write($"\nUncaught exception 0x{code:D2} at l{lineIndex}: {message}");
								if (!Program.stopAsking)
								{
									if (!OutputForm.ErrorMessage($"Line {lineIndex}: " + message))
									{
										Program.Exit("-1");
									}
								}
							}
							break;
					}
				}
			}
		}

		// LineCheck(): Splits the text into an array for further operations.
		internal bool LineCheck(ref string[] lineInfo, ref int lineIndex, bool disable = false)
		{
			if (line == null)
			{
				SendMessage(Level.ERR, "Unexpected NULL", 1);
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
						if (!disable)
						{
							ExtractEngineArgs(ref lineInfo);
						}
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

		internal void RemoveVariable(string variable)
		{
			if (localMemory.ContainsKey(variable))
			{
				localMemory.Remove(variable);
			}
			else
			{
				SendMessage(Level.WRN, $"Tried removing a non-existing variable {variable}.");
			}
		}

		internal void CreateVariable(string name, string data = "0")
		{
			if (predefinedVariables.ContainsKey(name[1..]))
			{
				SendMessage(Level.ERR, $"Variable {name[0]} is a predefined variable and cannot be declared.", 2);
				return;
			}
			if (localMemory.ContainsKey(name))
			{
				SendMessage(Level.WRN, $"Variable {name} already exists.");
				localMemory[name] = data;
			}
			else
			{
				localMemory.Add(name, data);
			}
		}

		internal string[] CreateVariables(ref string[] lineInfo)
		{
			string[] args = ExtractArgs(ref lineInfo);
			string[] cArgs = ExtractArgs(ref lineInfo, true);
			string varName = "", data = "NUll";
			int j = 0;
			List<string> generatedItems = new List<string>();
			for (int i = 0; i < args.Length; i++)
			{
				if (cArgs[i] == "|")
				{
					if (j == 1)
					{
						data = "0";
					}
					j = -1;
					generatedItems.Add(varName);
					CreateVariable(varName, data);
				}
				else
				{
					if (j == 0)
					{
						varName = args[i];
					}
					else if (j == 1)
					{
						data = args[i];
					}
				}
				j++;
			}
			if (j == 1)
			{
				data = "0";
			}
			generatedItems.Add(varName);
			CreateVariable(varName, data);
			return generatedItems.ToArray(); // List of generated items (which may be discarded if not for use) for USING.
		}

		internal void CallFunction(string file, string entryPoint, string[] excessArg, int toIgnore)
		{
			childProcess = new Engine(file, entryPoint);
			if (childProcess.Executable())
			{
				string[] programArgs = new string[excessArg[toIgnore..].Length];
				for (int i = toIgnore; i < excessArg.Length; i++)
				{
					programArgs[i - toIgnore] = excessArg[i];
				}
				childProcess.arguments = programArgs;
				returnedValue = childProcess.StartExecution();
			}
			else
			{
				SendMessage(Level.ERR, "The file does not exist.", 8);
			}
			childProcess = null;
		}

		// StartExecution(): "Entry point" to the program. This goes line by line, and executes instructions.
		internal string StartExecution(bool jump = false, int jumpLine = 0)
		{
			lineIndex = 0;
			sr = new StreamReader(scriptFile);
			if (jump)
			{
				JumpToLine(ref sr, ref line, ref lineIndex, ref jumpLine);
			}
			else if (!FindProgram(ref sr, ref line, ref lineIndex))
			{
				// unknown error
				Console.WriteLine("Unknown error");
			}
			while ((line = sr.ReadLine()) != null)
			{
				lock(Program.internalShared.SyncRoot)
				{
					if (Program.internalShared[0] == "FALSE")
					{
						SendMessage(Level.ERR, $"Internal window process has to close due to {Program.internalShared[1]}.", 3);
						OutputForm.app.Dispose();
						Program.SettingGraphicsMode = Mode.DISABLED;
					}
				}

				lineIndex++;

				if (LineCheck(ref lineInfo, ref lineIndex))
				{
					continue;
				}

				string[] args = ExtractArgs(ref lineInfo);
				string instructionName = lineInfo[0].ToUpper();
				switch (instructionName)
				{
					case "": // Empty
						break;

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

					case "EDEF": // Shorter
					case "ENDDEF": // Possible end-of-function
						if (lineInfo[1] == entryPoint)
						{
							return "0";
						}
						else
						{
							SendMessage(Level.ERR, "Unexpected end of definition, expect unwanted side effects.", 4);
						}
						break;

					case "CLEAR":
						if (args != null)
						{
							int imax = Program.Parse<int>(args[0]);
							Console.SetCursorPosition(0, Console.CursorTop);
							Console.Write(new string(' ', Console.BufferWidth));
							for (int i = 1; i < imax; i++)
							{
								Console.SetCursorPosition(0, Console.CursorTop - 2);
								Console.Write(new string(' ', Console.BufferWidth));
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

					case "NEW": // Splitter is comma
						{
							CreateVariables(ref lineInfo);
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
								JumpToLine(ref ifsr, ref line, ref ifLineIndex, ref lineIndex);
								while ((line = ifsr.ReadLine()) != null)
								{
									ifLineIndex++;
									if (LineCheck(ref cLineInfo, ref ifLineIndex, true))
									{
										continue;
									}
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
								ifsr.Dispose();
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
									SendMessage(Level.ERR, "Could not find a spot to jump to.", 5);
								}
							}
						}
						break;

					case "ENDIF": // To be ignored
						break;

					case "ELSE":
						StatementJumpOut("ENDIF", "IF");
						break;

					case "SET":
						if (localMemory.ContainsKey(args[0]))
						{
							localMemory[args[0]] = args[1];
						}
						else
						{
							SendMessage(Level.ERR, $"The variable {args[0]} does not exist.", 6);
						}
						break;

					case "DEL":
						foreach (string variable in args)
						{
							RemoveVariable(variable);
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
					case "CMP": // CMP shorter
						{
							compareOutput = Compare(ref args);
						}
						break;

					case "ROUND":
						{
							string varName, num1IN, sizeIN;
							if (args.Length > 2)
							{
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
							string output = Math.Round(Parse<decimal>(num1IN), Parse<int>(sizeIN)).ToString();
							SetVariable(args[0], ref output);
						}
						break;

					case "COLRGBTOHEX":
					case "RGBTOHEX": // RGBTOHEX preferred instruction for Maartanic
						{
							string varName = args[0];
							string output = $"{Parse<int>(args[1]):X2}{Parse<int>(args[2]):X2}{Parse<int>(args[3]):X2}";
							SetVariable(varName, ref output);
						}
						break;

					case "HEXTORGB":
						{
							string[] varNames = new string[3] { args[0], args[1], args[2] };
							string[] colorsOut = new string[3];

							Color colorOutput;
							colorOutput = Program.HexHTML(args[3]);

							colorsOut[0] = colorOutput.R.ToString();
							colorsOut[1] = colorOutput.G.ToString();
							colorsOut[2] = colorOutput.B.ToString();
							for (int i = 0; i < 3; i++)
							{
								SetVariable(varNames[i], ref colorsOut[i]);
							}
						}
						break;

					case "RAND":
						{
							string varName = args[0];
							Random generator = new Random();
							string output = generator.Next(Parse<int>(args[1]), Parse<int>(args[2]) + 1).ToString();
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
								n = Parse<decimal>(args[1]);
							}
							else
							{
								output = '$' + varName;
								LocalMemoryGet(ref output);
								n = Parse<decimal>(output);
							}
							output = Math.Abs(n).ToString();
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
						KeyOutput = (Program.GetAsyncKeyState(VK.ConvertKey(args[0])) != 0) && (hasInternalAccess || Program.IsFocused());
						break;

					case "HLT":
						SendMessage(Level.INF, "HLT");
						sr.Dispose();
						Program.Exit("2");
						break;

					case "SUBSTR":
						{
							string input, output;
							int start, len;
							if (args.Length > 3)
							{
								input = args[1];
								start = Parse<int>(args[2]);
								len = Parse<int>(args[3]);
							}
							else
							{
								input = '$' + args[0];
								LocalMemoryGet(ref input);
								start = Parse<int>(args[1]);
								len = Parse<int>(args[2]);
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
								index = Parse<int>(args[2]);
							}
							else
							{
								input = '$' + args[0];
								LocalMemoryGet(ref input);
								index = Parse<int>(args[1]);
							}
							if (index < 0 || index >= input.Length)
							{
								SendMessage(Level.ERR, $"Index {index} is out of bounds.", 7);
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
							input = Regex.Replace(input.Trim(), @"\s+", " "); // Trim and remove duplicate spaces
							SetVariable(args[0], ref input);
						}
						break;

					case "DO":
					case "CALL":
						{
							CallFunction(scriptFile, args[0], args, 1);
						}
						break;

					case "RET":
						if (args != null && args.Length > 0)
						{
							return args[0];
						}
						return "5"; // Manual close 5: return code

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
					case "SQRT":
						MathFunction(instructionName, args[0], args.Length > 1 ? args[1] : null);
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
								SendMessage(Level.ERR, "Stack was empty and could not be popped from.", 9);
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
								SendMessage(Level.ERR, "Queue was empty and could not be dequeued from.", 9);
							}
							SetVariable(args[0], ref output);
						}
						break;

					case "ALOC":
						for (int i = 0; i < Parse<int>(args[0]); i++)
						{
							Program.memory.Add("0");
						}
						break;

					case "FREE":
						for (int i = 0; i < Parse<int>(args[0]); i++)
						{
							if (!Program.memory.Exists(0))
							{
								SendMessage(Level.WRN, "Tried freeing memory that doesn't exist.", 6);
								continue;
							}
							else
							{
								Program.memory.Remove(1);
							}
						}
						break;

					case "SETM":
						{
							SetMemoryAddr(Parse<int>(args[0]), args[1]);
						}
						break;

					case "GETM":
						{
							Program.memory.Get(Parse<int>(args[1]), out string output);
							SetVariable(args[0], ref output);
						}
						break;

					case "REDRAWOK":
						redraw = false;
						break;

					default:
						if (Program.SettingExtendedMode == Mode.ENABLED) // Enable extended instruction set
						{
							string output = Program.extendedMode.Instructions(this, ref lineInfo, ref args);
							if (output != null)
							{
								sr.Dispose();
								return output;
							}
						}
						if (Program.SettingGraphicsMode == Mode.ENABLED && (Program.SettingExtendedMode != Mode.ENABLED || !Program.extendedMode.recognizedInstruction))
						{
							string output = GraphicsInstructions.Instructions(this, ref lineInfo, ref args);
							if (output != null)
							{
								sr.Dispose();
								return output;
							}
						}
						else if (Program.SettingExtendedMode == Mode.ENABLED && !Program.extendedMode.recognizedInstruction)
						{
							SendMessage(Level.ERR, $"Unrecognized instruction \"{lineInfo[0]}\". (VSB)", 10);
						}
						break;
				}
			}
			sr.Dispose(); // Close StreamReader after execution
			return returnedValue;
		}

		internal int GetJumpNr(string endNaming, string startNaming)
		{
			int scope = 0;
			bool success = false;
			int whileLineIndex = 0;
			string[] cLineInfo = null;
			StreamReader endifsr = new StreamReader(scriptFile);
			while ((line = endifsr.ReadLine()) != null)
			{
				whileLineIndex++;
				if (LineCheck(ref cLineInfo, ref whileLineIndex, true))
				{
					continue;
				}
				if (whileLineIndex > lineIndex)
				{
					if (cLineInfo[0].ToUpper() == endNaming && scope == 0)
					{
						success = true;
						break;
					}
					if (cLineInfo[0].ToUpper() == startNaming)
					{
						scope++;
					}
					if (cLineInfo[0].ToUpper() == endNaming)
					{
						scope--;
					}
				}

			}
			if (success)
			{
				return whileLineIndex;
			}
			else
			{
				SendMessage(Level.ERR, $"Could not jump to end of {startNaming}.", 5);
				return 0;
			}
		}

		// StatementJumpOut(): Jumps out of the statement.
		internal void StatementJumpOut(string endNaming, string startNaming)
		{
			int scope = 0;
			bool success = false;
			int whileLineIndex = 0;
			string[] cLineInfo = null;
			StreamReader endifsr = new StreamReader(scriptFile);
			while ((line = endifsr.ReadLine()) != null)
			{
				whileLineIndex++;
				if (LineCheck(ref cLineInfo, ref whileLineIndex, true))
				{
					continue;
				}
				if (whileLineIndex > lineIndex)
				{
					if (cLineInfo[0].ToUpper() == endNaming && scope == 0)
					{
						success = true;
						break;
					}
					if (cLineInfo[0].ToUpper() == startNaming)
					{
						scope++;
					}
					if (cLineInfo[0].ToUpper() == endNaming)
					{
						scope--;
					}
				}

			}
			if (success)
			{
				SendMessage(Level.INF, "Continuing at line " + whileLineIndex);
				for (int i = lineIndex; i < whileLineIndex; i++)
				{
					if ((line = sr.ReadLine()) == null)
					{
						break; // safety protection?
					}
				}
				lineIndex = whileLineIndex;
			}
			else
			{
				SendMessage(Level.ERR, $"Could not jump to end of {startNaming}.", 5);
			}
		}

		// JumpToLine(): Jumps to a line in the streamreader.
		internal void JumpToLine(ref StreamReader sr, ref string line, ref int lineIndex, ref int jumpLine)
		{
			while (((line = sr.ReadLine()) != null) && lineIndex < jumpLine-1)
			{
				lineIndex++;
			}
			if (lineIndex >= jumpLine-1)
			{
				lineIndex++;
			}
			else
			{
				SendMessage(Level.ERR, $"Unable to jump to line {jumpLine}", 5);
			}
		}

		// SetMemoryAddr(): Sets a given memory address to the given value. 
		private void SetMemoryAddr(int address, string value)
		{
			address = Program.SettingExtendedMode == Mode.DISABLED ? address - 1 : address;
			if (!Program.memory.Exists(address))
			{
				SendMessage(Level.ERR, $"Memory address {address} does not exist.", 6);
			}
			else
			{
				Program.memory.Set(address, value);
			}
		}

		// ToRadians(): Converts a given angle in degrees to radians with a limited amount of accuracy
		private double ToRadians(double input)
		{
			return 0.01745329251 * input;
		}

		// ToDegrees(): Converts a given angle in radians to degrees with a limited amount of accuracy
		private double ToDegrees(double input)
		{
			return 57.2957795131 * input;
		}

		// MathFunction(): Method merges multiple cases in the big switch of StartExecution().
		private void MathFunction(string function, string destination, string number)
		{
			double dnum;
			if (number == null)
			{
				string tmp = '$' + destination;
				LocalMemoryGet(ref tmp);
				dnum = Parse<double>(tmp);
			}
			else
			{
				dnum = Parse<double>(number);
			}
			double result;
			switch (function)
			{
				case "COS": // To radians, use it, and back to degrees.
					result = Math.Cos(ToRadians(dnum));
					break;

				case "SIN":
					result = Math.Sin(ToRadians(dnum));
					break;

				case "TAN":
					result = Math.Tan(ToRadians(dnum));
					break;

				case "ACOS":
					result = Math.Acos(ToRadians(dnum));
					break;

				case "ASIN":
					result = Math.Asin(ToRadians(dnum));
					break;

				case "ATAN":
					result = Math.Atan(ToRadians(dnum));
					break;

				case "LOG": // Log w/ base 10
					result = Math.Log10(dnum);
					break;

				case "MATH_LN": // Natural logarithm (e as base)
					result = Math.Log(dnum);
					break;

				case "EPOW":
					result = Math.Exp(dnum);
					break;

				case "TENPOW":
					result = Math.Pow(10, dnum);
					break;

				case "TORAD":
					result = ToRadians(dnum);
					break;

				case "TODEG":
					result = ToDegrees(dnum);
					break;

				case "FLR":
					result = Math.Floor(dnum);
					break;

				case "CEIL":
					result = Math.Ceiling(dnum);
					break;

				case "SQRT":
					result = Math.Sqrt(dnum);
					break;

				default:
					result = 0.0d;
					SendMessage(Level.ERR, $"Unrecognized function {function}.", 10);
					break;
			}
			string resultS = result.ToString();
			SetVariable(destination, ref resultS);
		}

		// PerformOp(): Performs an operation with two values given.
		private void PerformOp(string operation, string varName, string num1, string num2)
		{
			double numberA, numberB;
			if (num2 == null)
			{
				string num1_var = '$' + varName;
				LocalMemoryGet(ref num1_var);
				numberA = Parse<double>(num1_var);
				numberB = Parse<double>(num1);
			}
			else
			{
				numberA = Parse<double>(num1);
				numberB = Parse<double>(num2);
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
					SendMessage(Level.ERR, $"Unrecognized operation {operation}.", 10);
					break;
			}
			SetVariable(varName, ref result);
		}

		// Compares two values inside the args array, and stores the result in compareOutput.
		internal bool Compare(ref string[] args)
		{
			bool r; // Output variable (result)
			bool b1, b2;
			// Numbers
			b1 = args[1].ToUpper() == "TRUE" || args[1] == "1" || args[1] == "1.0";
			b2 = args[2].ToUpper() == "TRUE" || args[2] == "1" || args[2] == "1.0";
			double n1 = Parse<double>(args[1], true), n2 = Parse<double>(args[2], true);

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
					SendMessage(Level.ERR, $"Unrecognized CMPR option {args[0].ToUpper()}.", 10);
					break;

			}
			return r;
		}

		// MathOperation(): Calculator
		private double MathOperation(char op, string destination, string number, string optnumber = null)
		{
			double num1, num2;
			if (optnumber == null)
			{
				string tmp1 = "$" + destination;
				LocalMemoryGet(ref tmp1);
				num1 = Parse<double>(tmp1);
				num2 = Parse<double>(number);
			}
			else
			{
				num1 = Parse<double>(number);
				num2 = Parse<double>(optnumber);
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
					SendMessage(Level.ERR, $"Unrecognized operator {op} used", 10);
					return 0.0d;
			}
		}

		// SetVariable(): Sets the variable with the name varName to newData. Lets the user know if it doesn't exist.
		internal void SetVariable(string varName, ref string newData)
		{
			if (localMemory.ContainsKey(varName))
			{
				localMemory[varName] = newData;
			}
			else
			{
				SendMessage(Level.ERR, $"The variable {varName} does not exist.", 6);
			}
		}

		internal Engine GetLatestChild()
		{
			if (childProcess != null)
				return childProcess.GetLatestChild();
			return this;
		}

		// LocalMemoryGet(): Converts a given variable to its contents. Leaves it alone if it doesn't have a recognized prefix.
		internal void LocalMemoryGet(ref string varName)
		{
			if (varName.Length == 0)
			{
				varName = "NULL";
				SendMessage(Level.ERR, "Malformed variable", 11);
				return;
			}
			if (varName[0] == '$')
			{
				if (varName[1] == '_')
				{
					if (predefinedVariables.ContainsKey(varName[2..]))
					{
						varName = predefinedVariables[varName[2..]](GetLatestChild());
					}
				}
				else if (localMemory.ContainsKey(varName[1..]))
				{
					varName = localMemory[varName[1..]];
				}
				else
				{
					SendMessage(Level.ERR, $"The variable {varName[1..]} does not exist.", 6);
					varName = "NULL";
				}
			}
			else if (Program.SettingExtendedMode == Mode.ENABLED)
			{
				switch(varName[0])
				{

					case '#': // Get memory address e.g. where A is the memory address: #A
						{
							int address = Parse<int>(varName[1..]);
							if (Program.memory.Exists(address))
							{
								Program.memory.Get(address, out varName);
							}
							else
							{
								SendMessage(Level.ERR, $"Tried accessing unallocated memory space {address}.", 6);
								varName = "NULL";
							}
						}
						break;

					case '%': // Get char at index A of string B: %A,B
						if (varName.Contains('.'))
						{
							string variable = varName[(varName.IndexOf('.') + 1)..];
							string index = varName[..varName.IndexOf('.')][1..];
							LocalMemoryGet(ref variable);
							LocalMemoryGet(ref index);
							varName = variable[Parse<int>(index)].ToString();
						}
						else
						{
							SendMessage(Level.ERR, $"Corrupted variable name syntax {varName} for index.", 11);
							varName = "NULL";
						}
						break;

					case '!': // Inverse statement e.g. where A is true, it will become false: !A
						{
							string variable = varName[1..];
							LocalMemoryGet(ref variable);
							variable = variable.ToLower();
							bool statement = variable == "true" || variable == "1";
							varName = (!statement).ToString().ToLower();
						}
						break;

					case '@':
						{
							int address = Parse<int>(varName[1..]);
							if (arguments != null)
							{
								if (address < arguments.Length)
								{
									varName = arguments[address];
								}
								else
								{
									varName = "NULL";
									SendMessage(Level.ERR, $"Argument @{address} does not exist.", 6);
								}
							}
						}
						break;
					default:
						break;
				}
			}
		}

		// ExtractArgs(): Simply extracts the arguments from array lineInfo, treating quote blocks as one.
		internal string[] ExtractArgs(ref string[] lineInfo, bool raw = false)
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
			string[] RetResult = new string[100]; //INFO This is the max amount of arguments allowed before it overflows...
			bool[] RetResultString = new bool[100];
			int RetResultPos = 0;
			string newCombined = "";
			bool isInQuotes = false;
			try
			{
				for (int i = 0; i < combined.Length; i++)
				{
					if (combined[i] == '"')
					{
						if (isInQuotes)
						{
							if (combined[i - 1] != '\\')
							{
								RetResultString[RetResultPos] = true;
								isInQuotes = false;
								RetResult[RetResultPos++] = newCombined;
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
					else if (combined[i] == '\\' && combined[i - 1] == '\\' && combined[i - 2] != '\\')
					{
						continue;
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
								RetResultString[RetResultPos] = false;
								RetResult[RetResultPos++] = newCombined;
								newCombined = "";
							}
							continue;
						}
						newCombined += combined[i];
					}
					if (i == combined.Length - 1)
					{
						RetResultString[RetResultPos] = false;
						RetResult[RetResultPos++] = newCombined;
						newCombined = "";
					}
				}
			}
			catch (IndexOutOfRangeException)
			{
				SendMessage(Level.ERR, "Reached max amount of arguments per instruction.", 12);
			}
			string[] finalOutput = new string[RetResultPos];
			{ // Make scope
				for (int i = 0; i < RetResultPos; i++)
				{
					if (RetResultString[i] || raw)
					{
						if (RetResultString[i] && raw)
							finalOutput[i] = '"' + RetResult[i] + '"';
						else
						{
							finalOutput[i] = RetResult[i];
						}
						continue;
					}
					LocalMemoryGet(ref RetResult[i]);
					finalOutput[i] = RetResult[i];
				}
			}
			return finalOutput;
		}

		internal void EnableGraphics()
		{
			if (Program.SettingGraphicsMode == Mode.DISABLED)
			{
				bool isAvailable = false;
				byte times = 1;
				while (!isAvailable)
				{
					lock (Program.internalShared.SyncRoot)
					{
						isAvailable = Program.internalShared[3] == "TRUE";
					}
					if (!isAvailable)
					{
						times++;
						if (times > 254)
						{
							break;
						}
						SendMessage(Level.INF, "Screen component is still loading.");
						Thread.Sleep(4);
					}
					else
					{
						break;
					}
				}
				if (!isAvailable)
				{
					SendMessage(Level.ERR, "Screen component took too long to load.", 13);
				}
				else
				{
					lock (Program.internalShared.SyncRoot)
					{
						Program.internalShared[2] = "TRUE";
					}
					Program.windowProcess.Interrupt();
					bool okToExit = false;
					times = 1;
					SendMessage(Level.INF, "Waiting for screen component response..");
					while (!okToExit)
					{
						lock (Program.internalShared.SyncRoot)
						{
							okToExit = !Parse<bool>(Program.internalShared[2]);
						}
						Thread.Sleep(4);
						times++;
						if (times > 254)
						{
							SendMessage(Level.ERR, "Screen component did not respond.", 14);
							break;
						}
					}
				}
				Program.SettingGraphicsMode = Mode.ENABLED;
			}
		}

		// ExtractEngineArgs(): Extracts [A B] like stuff and applies it to internal engine variables.
		private void ExtractEngineArgs(ref string[] lineInfo)
		{
			string[] engineArgParts;
			string engineArg = "";
			foreach (string part in lineInfo)
			{
				engineArg += ' ' + part;
			}
			engineArgParts = engineArg[1..].Trim('[', ']').Split(' ');
			switch (engineArgParts[0].ToLower())
			{
				case "mode":
					switch (engineArgParts[1].ToLower())
					{
						case "vsb":
							if (Program.SettingExtendedMode == Mode.ENABLED) // Disable
							{
								Program.extendedMode.Dispose(); // Destruct extended mode, thus freeing up memory
								Program.SettingExtendedMode = Mode.DISABLED;
								if (hasInternalAccess)
								{
									hasInternalAccess = false; // Disable real mode
									SendMessage(Level.INF, "Real mode disabled.");
								}	
								SendMessage(Level.INF, "Using compat/vsb mode");
							}
							Program.ShowWindow(Program.GetConsoleWindow(), 5);
							break;

						case "extended":
							if (Program.SettingExtendedMode == Mode.DISABLED) // Enable
							{
								Program.extendedMode = new ExtendedInstructions();
								Program.SettingExtendedMode = Mode.ENABLED;
								SendMessage(Level.INF, "Using extended mode");
							}
							break;

						default:
							SendMessage(Level.ERR, "Unrecognized engine option mode.", 10);
							break;
					}
					break;

				case "graphics":
					switch (engineArgParts[1].ToLower())
					{
						case "enable":
							EnableGraphics();
							SendMessage(Level.INF, "Graphics enabled");
							break;

						case "disable":
							if (Program.SettingGraphicsMode == Mode.ENABLED)
							{
								MinimizeWindow();
								Program.SettingGraphicsMode = Mode.DISABLED;
								SendMessage(Level.INF, "Graphics disabled");
							}
							break;

						default:
							SendMessage(Level.ERR, "Unrecognized graphics option mode.", 10);
							break;
					}
					break;

				default:
					SendMessage(Level.ERR, "Unrecognized engine option.", 10);
					break;
			}
		}
	}
}
