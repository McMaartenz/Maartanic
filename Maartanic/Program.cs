﻿using System;
using System.Drawing;
using System.Threading;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Maartanic
{
	public class Program
	{
		//BUG null reference, most instructions require arguments but if none are given it returns a null reference exception. Or you as programmer should just know what you are doing.  Your fault if it crashes.
		//TODO An error should raise an event. This is why we need a try catch block.
		//BUG nested USING statements do not work.

		internal const float VERSION = 1.2f;
		internal static int WIN_WIDTH = 480; // EngineGraphics class require these, therefore they must be defined before graphics.
		internal static int WIN_HEIGHT = 360;

		internal static EngineStack stack = new EngineStack();
		internal static EngineQueue queue = new EngineQueue();
		internal static EngineMemory memory = new EngineMemory();
		internal static EngineGraphics graphics = new EngineGraphics();

		internal static string[] internalShared = new string[5]
		{
			"TRUE",		// isRunning? Threads should close when this is "FALSE"
			"NULL",		// Reason isRunning is set to false
			"FALSE",	// If window is ready to show
			"FALSE",	// If window process is ready to be interrupted
			"FALSE"		// If EN is initialized properly
		};

		internal static ExtendedInstructions extendedMode;
		internal static Engine.Mode SettingExtendedMode = Engine.Mode.DISABLED;
		internal static Engine.Mode SettingGraphicsMode = Engine.Mode.DISABLED;

		internal static int CON_WIDTH = 120;
		internal static int CON_HEIGHT = 30;

		// Default 480x360

		internal static Thread consoleProcess;
		internal static Thread windowProcess;

		internal static byte logLevel;
		internal static Engine EN;

		internal static bool stopAsking = false;


		// PInvoke
		[DllImport("kernel32.dll")]
		internal static extern IntPtr GetConsoleWindow();

		[DllImport("user32.dll")]
		internal static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		[DllImport("user32.dll")]
		internal static extern int GetAsyncKeyState(int vKeys);

		[DllImport("user32.dll")]
		internal static extern bool OpenClipboard(IntPtr hWndNewOwner);

		[DllImport("user32.dll")]
		internal static extern bool CloseClipboard();

		[DllImport("user32.dll")]
		internal static extern bool SetClipboardData(uint uFormat, IntPtr data);

		internal static void SetClipboard(string text)
		{
			OpenClipboard(IntPtr.Zero);
			SetClipboardData(13, Marshal.StringToHGlobalUni(text));
			CloseClipboard();
		}

		internal static bool IsFocused()
		{
			return GetForegroundWindow() == GetConsoleWindow() || GetForegroundWindow() == OutputForm.GetHandle(OutputForm.app);
		}

		// Exit(): Exit process
		internal static void Exit(string value)
		{
			string R = value switch
			{
				"-1" => "Process closed incorrectly. (code -1)",
				"0" => "Process sucessfully closed. (code 0)",
				"1" => "Process closed due to an internal thread. (code 1)",
				"2" => "Process was manually halted. (code 2)",
				"3" => "Process was closed due to a break statement (code 3)",
				"4" => "Process was closed due to a continue statement (code 4)",
				"5" => "Process succesfully closed. (RET) (code 5)",
				_ => $"Process closed with value {value}.",
			};
			Console.Write('\n' + R);
			Console.ReadLine();
			Environment.Exit(0);
		}

		internal static bool RequestPermission(Engine e)
		{
			if (e.hasInternalAccess)
			{
				return true;
			}
			if (OutputForm.app.RequestBox(e.entryPoint + " inside " + e.scriptFile))
			{
				e.hasInternalAccess = true;
				return true;
			}
			else
			{
				EN.SendMessage(Engine.Level.ERR, "Access denied from user.");
				return false;
			}
		}

		internal static T Parse<T> (string input, bool silence = false)
		{
			try
			{
				return (T)typeof(T).GetMethod("Parse", new[] { typeof(string) }).Invoke(null, new string[] { input });
			}
			catch (TargetInvocationException)
			{
				if (typeof(T) == typeof(bool))
				{
					return (T)Convert.ChangeType(input == "1", typeof(T));
				}
				if (!silence)
				{
					if (EN != null)
					{
						EN.SendMessage(Engine.Level.ERR, $"Malformed {typeof(T).Name} '{input}' found.", 11);
					}
					else
					{
						Console.Write($"\nINTERNAL MRT ERROR: Malformed {typeof(T).Name} '{input}' found.");
					}
				}
				return default;
			}
			catch (Exception ex)
			{
				Console.Write($"\nINTERNAL MRT ERROR: " + ex);
				return default;
			}
		}

		internal static Color HexHTML(string input)
		{
			input = input.Trim();
			if (input.StartsWith("0x"))
			{
				input = input[2..];
			}
			if (!input.StartsWith('#'))
			{
				input = '#' + input;
			}
			try
			{
				return ColorTranslator.FromHtml(input);
			}
			catch (ArgumentException)
			{
				EN.SendMessage(Engine.Level.ERR, $"Malformed hexadecimal '0x{input[1..]}' found.", 11);
				return default;
			}
		}

		// Main(): Entry point
		public static void Main(string[] args)
		{			
			consoleProcess = Thread.CurrentThread; // Current thread
			consoleProcess.Name = "consoleProcess";

			ThreadStart formWindowStarter = new ThreadStart(OutputForm.Main); // Window thread
			windowProcess = new Thread(formWindowStarter)
			{
				Name = "windowProcess"
			};

			if (!windowProcess.TrySetApartmentState(ApartmentState.STA))
			{
				Console.WriteLine("Could not switch window thread apartment state to STA.");
				Exit("-1");
			}
			windowProcess.Start();

			Console.SetBufferSize(CON_WIDTH, CON_HEIGHT); // Remove scrollbar
			Console.SetWindowSize(CON_WIDTH, CON_HEIGHT);

			Console.Title = $"Maartanic Engine {VERSION}";

			Console.WriteLine("Maartanic Engine {0} (gui VSB Engine Emulator on C#)\n", VERSION);
			if (args.Length == 0)
			{
				ThreadStart fileBrowserStarter = new ThreadStart(FileBrowser.Main);
				Thread fileBrowserProcess = new Thread(fileBrowserStarter)
				{
					Name = "fileBrowserProcess",
				};
				if (!fileBrowserProcess.TrySetApartmentState(ApartmentState.STA))
				{
					Console.WriteLine("Could not switch file browser thread apartment state to STA.");
					Exit("-1");
				}
				fileBrowserProcess.Start();
				fileBrowserProcess.Join();
				if (FileBrowser.returnedFile == null)
				{
					Console.WriteLine("Canceled.");
					Exit("0");
				}
				else
				{
					args = new string[] { FileBrowser.returnedFile };
				}
			}

			Console.WriteLine("Please enter the log level (0: info 1: warning 2: error");
			logLevel = Parse<byte>(Console.ReadLine());
			if (logLevel < 0 || logLevel > 3)
			{
				logLevel = 0;
			}

			// Clear buffer
			Console.Clear();
			EN = new Engine(args[0]);
			EN.FillPredefinedList();
			if (OutputForm.StartWithGraphics())
			{
				EN.EnableGraphics();
			}
			if (EN.Executable())
			{
				string returnVariable = "";
				try
				{
					do
					{
						returnVariable = EN.StartExecution();
					} while (SettingExtendedMode == Engine.Mode.DISABLED && returnVariable != "5");
					EN.sr.Close();
					EN.sr.Dispose();
					Exit(returnVariable);
				}
				catch (Exception ex)
				{
					Console.Write("\n\n\nINTERNAL MRT ERROR\n\n" + ex.ToString());
					OutputForm.CriticalError(ex.Message + " More information can be found in the console.");
				}
			}
			Exit("0");
		}
	}
}
