namespace TickToTalk
{
    // Driven by authoritative ground contact, never by the lagging visual transform.
    public sealed class PlayerMotionState
    {
        public const float TakeoffDuration = .12f, LandingDuration = .24f;
        bool airborne;
        float tookOffAt, landedAt = -100;
        public string AppliedState;
        public string Select(float height, float speed, bool running, float now)
        {
            bool inAir = height > .02f;
            if (inAir && !airborne) tookOffAt = now;
            if (!inAir && airborne) landedAt = now;
            airborne = inAir;
            if (inAir) return now - tookOffAt < TakeoffDuration ? "Jump_Start" : "Jump_Loop";
            if (now - landedAt < LandingDuration) return "Jump_End";
            return speed > .1f ? (running ? "Run_Forward" : "Walk_Forward") : "Idle_Breathing";
        }
        public void Reset() { airborne = false; landedAt = -100; AppliedState = null; }
    }
}
