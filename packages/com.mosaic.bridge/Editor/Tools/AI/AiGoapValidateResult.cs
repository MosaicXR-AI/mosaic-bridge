namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiGoapValidateResult
    {
        public string[]              AchievableGoals   { get; set; }
        public UnachievableGoalInfo[] UnachievableGoals { get; set; }
        public string[]              OrphanedActions   { get; set; }
        public bool                  IsValid           { get; set; }
    }

    public sealed class UnachievableGoalInfo
    {
        public string Goal          { get; set; }
        public string MissingEffect { get; set; }
    }
}
