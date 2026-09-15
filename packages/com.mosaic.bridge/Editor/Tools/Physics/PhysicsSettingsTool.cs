using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsSettingsTool
    {
        private const string AssetPath = "ProjectSettings/DynamicsManager.asset";

        [MosaicTool("physics/settings",
                    "Gets or sets project-wide 3D physics settings (ProjectSettings/DynamicsManager.asset): " +
                    "Gravity (folds physics/set-gravity's behavior in), BounceThreshold, " +
                    "DefaultContactOffset, solver iterations, SleepThreshold, SimulationMode, " +
                    "QueriesHitTriggers/Backfaces, AutoSyncTransforms.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<PhysicsSettingsResult> Execute(PhysicsSettingsParams p)
        {
            var target = AssetDatabase.LoadMainAssetAtPath(AssetPath);
            if (target == null)
                return ToolResult<PhysicsSettingsResult>.Fail($"Could not load '{AssetPath}'", ErrorCodes.NOT_FOUND);

            var so = new SerializedObject(target);
            bool changed = false;

            if (p.Gravity != null)
            {
                if (p.Gravity.Length != 3)
                    return ToolResult<PhysicsSettingsResult>.Fail("Gravity requires exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
                var gravityProp = so.FindProperty("m_Gravity");
                gravityProp.FindPropertyRelative("x").floatValue = p.Gravity[0];
                gravityProp.FindPropertyRelative("y").floatValue = p.Gravity[1];
                gravityProp.FindPropertyRelative("z").floatValue = p.Gravity[2];
                changed = true;
            }
            if (p.BounceThreshold.HasValue)
            {
                so.FindProperty("m_BounceThreshold").floatValue = p.BounceThreshold.Value;
                changed = true;
            }
            if (p.DefaultContactOffset.HasValue)
            {
                so.FindProperty("m_DefaultContactOffset").floatValue = p.DefaultContactOffset.Value;
                changed = true;
            }
            if (p.DefaultSolverIterations.HasValue)
            {
                so.FindProperty("m_DefaultSolverIterations").intValue = p.DefaultSolverIterations.Value;
                changed = true;
            }
            if (p.DefaultSolverVelocityIterations.HasValue)
            {
                so.FindProperty("m_DefaultSolverVelocityIterations").intValue = p.DefaultSolverVelocityIterations.Value;
                changed = true;
            }
            if (p.SleepThreshold.HasValue)
            {
                so.FindProperty("m_SleepThreshold").floatValue = p.SleepThreshold.Value;
                changed = true;
            }
            if (p.DefaultMaxDepenetrationVelocity.HasValue)
            {
                so.FindProperty("m_DefaultMaxDepenetrationVelocity").floatValue = p.DefaultMaxDepenetrationVelocity.Value;
                changed = true;
            }
            if (p.DefaultMaxAngularSpeed.HasValue)
            {
                so.FindProperty("m_DefaultMaxAngularSpeed").floatValue = p.DefaultMaxAngularSpeed.Value;
                changed = true;
            }
            if (!string.IsNullOrEmpty(p.SimulationMode))
            {
                if (!System.Enum.TryParse<SimulationMode>(p.SimulationMode, ignoreCase: true, out var mode))
                    return ToolResult<PhysicsSettingsResult>.Fail(
                        $"Unknown SimulationMode '{p.SimulationMode}'. Valid: FixedUpdate, Update, Script",
                        ErrorCodes.INVALID_PARAM);
                so.FindProperty("m_SimulationMode").intValue = (int)mode;
                changed = true;
            }
            if (p.QueriesHitTriggers.HasValue)
            {
                so.FindProperty("m_QueriesHitTriggers").boolValue = p.QueriesHitTriggers.Value;
                changed = true;
            }
            if (p.QueriesHitBackfaces.HasValue)
            {
                so.FindProperty("m_QueriesHitBackfaces").boolValue = p.QueriesHitBackfaces.Value;
                changed = true;
            }
            if (p.AutoSyncTransforms.HasValue)
            {
                so.FindProperty("m_AutoSyncTransforms").boolValue = p.AutoSyncTransforms.Value;
                changed = true;
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }

            var finalGravity = so.FindProperty("m_Gravity");
            return ToolResult<PhysicsSettingsResult>.Ok(new PhysicsSettingsResult
            {
                Gravity = new[]
                {
                    finalGravity.FindPropertyRelative("x").floatValue,
                    finalGravity.FindPropertyRelative("y").floatValue,
                    finalGravity.FindPropertyRelative("z").floatValue,
                },
                BounceThreshold        = so.FindProperty("m_BounceThreshold").floatValue,
                DefaultContactOffset   = so.FindProperty("m_DefaultContactOffset").floatValue,
                DefaultSolverIterations = so.FindProperty("m_DefaultSolverIterations").intValue,
                DefaultSolverVelocityIterations = so.FindProperty("m_DefaultSolverVelocityIterations").intValue,
                SleepThreshold         = so.FindProperty("m_SleepThreshold").floatValue,
                DefaultMaxDepenetrationVelocity = so.FindProperty("m_DefaultMaxDepenetrationVelocity").floatValue,
                DefaultMaxAngularSpeed = so.FindProperty("m_DefaultMaxAngularSpeed").floatValue,
                SimulationMode         = ((SimulationMode)so.FindProperty("m_SimulationMode").intValue).ToString(),
                QueriesHitTriggers     = so.FindProperty("m_QueriesHitTriggers").boolValue,
                QueriesHitBackfaces    = so.FindProperty("m_QueriesHitBackfaces").boolValue,
                AutoSyncTransforms     = so.FindProperty("m_AutoSyncTransforms").boolValue,
                Message                = changed ? "Physics settings updated" : "No changes (read-only query)",
            });
        }
    }
}
