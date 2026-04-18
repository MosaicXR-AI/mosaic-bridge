namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiGoapPlanResult
    {
        public PlanStep[] Plan         { get; set; }
        public float      PlanCost     { get; set; }
        public string     GoalSelected { get; set; }
        public bool       Success      { get; set; }
    }

    public sealed class PlanStep
    {
        public string ActionName     { get; set; }
        public string ResultingState { get; set; }
    }
}
