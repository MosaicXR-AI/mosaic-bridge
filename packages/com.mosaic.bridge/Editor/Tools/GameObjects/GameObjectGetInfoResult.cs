namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectGetInfoResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public bool ActiveSelf { get; set; }
        public bool ActiveInHierarchy { get; set; }
        public string[] Components { get; set; }
        public string Tag { get; set; }
        public string Layer { get; set; }
        public int ChildCount { get; set; }

        // The transform was missing entirely, which made a whole class of question unanswerable
        // from outside: "did the player actually move?" could not be checked, so a recording of
        // scenery moving while the player stood still passed as gameplay. Reading a position is
        // the cheapest possible verification and there was no way to do it.
        public float[] Position { get; set; }        // world, [x, y, z]
        public float[] LocalPosition { get; set; }
        public float[] Rotation { get; set; }        // world euler angles, degrees
        public float[] LocalScale { get; set; }
    }
}
