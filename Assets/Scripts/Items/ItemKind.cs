namespace RaceSabotage
{
    /// <summary>
    /// Design doc §4 priority order. Blind/SlowMotion/ButtonScramble aren't wired up
    /// yet - Racing has only one action button (jump), so any interference item there
    /// has to target information or time, not a key mapping. See §4.2.
    /// </summary>
    public enum ItemKind
    {
        Swap,
        Nitro,
        Mine
    }
}
