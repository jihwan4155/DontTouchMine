
namespace Game.Core.Match
{
    public static class HidingPhysicsGroups
    {
        public const int FirstLayer = 20;
        public const int SecondLayer = 21;
        public const int Mask = (1 << FirstLayer) | (1 << SecondLayer);
        public static int LayerFor(int playerIndex) => FirstLayer + (playerIndex & 1);
        public static int QueryMask(int layer, int mask = ~(1 << 2)) =>
            layer == FirstLayer ? mask & ~((1 << SecondLayer) | (1 << 6)) :
            layer == SecondLayer ? mask & ~((1 << FirstLayer) | (1 << 6)) : mask;

    }
}
