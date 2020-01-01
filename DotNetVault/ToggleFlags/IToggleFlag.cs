namespace DotNetVault.ToggleFlags
{
    internal interface IToggleFlag
    {
        bool IsSet { get; }

        bool IsClear { get; }

        bool SetFlag();

        bool ClearFlag();
    }
}
