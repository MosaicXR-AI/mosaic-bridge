namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationStateResult
    {
        public string Action { get; set; }
        public string ControllerPath { get; set; }
        public string StateName { get; set; }
        public int LayerIndex { get; set; }

        // -- info --
        public string MotionName { get; set; }
        public string MotionPath { get; set; }
        public float Speed { get; set; }
        public string Tag { get; set; }
        public int TransitionCount { get; set; }
        public bool IsDefault { get; set; }

        // -- add --
        public float PositionX { get; set; }
        public float PositionY { get; set; }

        // -- add-sub-machine --
        public string SubMachineName { get; set; }

        /// <summary>The "/"-separated path this state/sub-machine was resolved or created under.</summary>
        public string ParentStateMachinePath { get; set; }

        // -- set-settings / info -- (Speed above already covers AnimatorState.speed)
        public string SpeedParameter { get; set; }
        public bool SpeedParameterActive { get; set; }
        public float CycleOffset { get; set; }
        public string CycleOffsetParameter { get; set; }
        public bool CycleOffsetParameterActive { get; set; }
        public bool Mirror { get; set; }
        public string MirrorParameter { get; set; }
        public bool MirrorParameterActive { get; set; }
        public bool IKOnFeet { get; set; }
        public bool WriteDefaultValues { get; set; }

        // -- add-behaviour --
        public string AddedBehaviourTypeName { get; set; }
    }
}
