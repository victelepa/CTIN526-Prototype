namespace RaceSabotage
{
    /// <summary>
    /// Design doc §4 priority order. Racing has only one movement action (jump), so
    /// interference items target information or time rather than reversing movement.
    /// </summary>
    public enum ItemKind
    {
        Swap,
        Nitro,
        Mine,
        Blind
    }
}
