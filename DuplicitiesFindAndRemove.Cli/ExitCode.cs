namespace DuplicitiesFindAndRemove.Cli;

public readonly record struct ExitCode(int Value)
{
    public static readonly ExitCode Success = new(0);
    public static readonly ExitCode NoDuplicatesFound = new(1);
    public static readonly ExitCode Error = new(-1);

    public static implicit operator int(ExitCode exitCode) => exitCode.Value;
}