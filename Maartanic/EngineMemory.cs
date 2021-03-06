﻿using System.Collections.Generic;

internal class EngineMemory
{
	private readonly List<string> x;

	// EngineMemory(): Class constructor, creates a memory space
	internal EngineMemory()
	{
		x = new List<string>();
	}

	// Add(): Adds a value to the memory, and returns the memory address it's at
	internal int Add(string value)
	{
		x.Add(value);
		return Count();
	}

	// Remove(): Removes a given amount of the memory space
	internal void Remove(int amount)
	{
		x.RemoveRange(Count() - amount, amount);
	}

	// Set(): Sets the space at a given memory address to the given value
	internal void Set(int index, string value)
	{
		x[index] = value;
	}

	// Exists(): Returns whether or not the index is in bounds
	internal bool Exists(int index)
	{
		return Count() > index && index >= 0;
	}

	// Get(): Gets the data at a given memory address
	internal void Get(int index, out string output)
	{
		output = x[index];
	}

	// Count(): Gets the amount of memory allocated
	internal int Count()
	{
		return x.Count;
	}
}
