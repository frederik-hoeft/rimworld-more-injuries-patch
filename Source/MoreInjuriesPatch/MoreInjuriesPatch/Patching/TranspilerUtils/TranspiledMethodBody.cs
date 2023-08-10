using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace MoreInjuriesPatch.Patching.TranspilerUtils;

internal class TranspiledMethodBody
{
    private readonly List<CodeInstruction> _instructions = new();
    private readonly List<TranspilerCheckpoint> _checkpoints = new();

    private TranspiledMethodBody() { }

    public static TranspiledMethodBody Empty() => new();

    public TranspiledMethodBody DefineCheckpoint(string checkpoint)
    {
        if (_checkpoints.Any(x => x.Checkpoint.Equals(checkpoint)))
        {
            Logger.Error($"Error during transpiler checkpoint definition: key '{checkpoint}' already exists!");
            return this;
        }
        if (_checkpoints.Any(cp => cp.IsCompleted))
        {
            Logger.Warning($"Inconsistent state during transpiler checkpoint definition of key '{checkpoint}': transpilation already started!");
        }
        _checkpoints.Add(TranspilerCheckpoint.Create(checkpoint));
        return this;
    }

    public bool TryCompleteCheckpoint(string checkpoint)
    {
        int i = 0;
        for (; i < _checkpoints.Count && _checkpoints[i].IsCompleted; i++) { }
        if (i >= _checkpoints.Count)
        {
            Logger.Error($"Error while completing transpiler checkpoint '{checkpoint}': all checkpoints already completed!");
            return false;
        }
        string nextCheckpoint = _checkpoints[i].Checkpoint;
        if (!nextCheckpoint.Equals(checkpoint))
        {
            Logger.Error($"Error while completing transpiler checkpoint '{checkpoint}': prerequisite '{nextCheckpoint}' not met!");
            return false;
        }
        _checkpoints[i] = _checkpoints[i] with { IsCompleted = true };
        Logger.Log($"Successfully completed transpiler checkpoint '{checkpoint}'");
        return true;
    }

    public TranspiledMethodBody Append(CodeInstruction instruction)
    {
        _instructions.Add(instruction);
        return this;
    }

    public TranspiledMethodBody Append(OpCode opcode, object? operand = null)
    {
        _instructions.Add(new CodeInstruction(opcode, operand));
        return this;
    }

    public bool TryFlush(out IReadOnlyCollection<CodeInstruction>? instructions)
    {
        if (_checkpoints.All(cp => cp.IsCompleted))
        {
            instructions = _instructions;
            return true;
        }
        TranspilerCheckpoint failedCheckpoint = _checkpoints.First(cp => !cp.IsCompleted);
        Logger.Error($"Error while flushing method body: '{failedCheckpoint.Checkpoint}' was never reached!");
        instructions = null;
        return false;
    }
}
