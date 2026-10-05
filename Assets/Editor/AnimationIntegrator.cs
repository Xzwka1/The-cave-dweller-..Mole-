using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace CaveDweller.EditorTools
{
    [InitializeOnLoad]
    public static class AnimationIntegrator
    {
        [MenuItem("Tools/Restart MCP Server")]
        public static void RestartMCPServer()
        {
            var serverType = System.Type.GetType("UnityMCP.Editor.MCPBridgeServer, AnkleBreaker.UnityMCP.Editor");
            if (serverType != null)
            {
                var stopMethod = serverType.GetMethod("Stop", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new[] { typeof(bool) }, null);
                if (stopMethod != null) stopMethod.Invoke(null, new object[] { true });
                else serverType.GetMethod("Stop", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, System.Type.EmptyTypes, null)?.Invoke(null, null);

                var startMethod = serverType.GetMethod("Start", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, System.Type.EmptyTypes, null);
                startMethod?.Invoke(null, null);
            }
        }

        [MenuItem("Tools/Integrate Animations")]
        public static void Execute()
        {
            Debug.Log("[AnimationIntegrator] Starting Animation & Controller consolidation...");

            IntegrateCharacterController();
            IntegrateMonsterController();
            UpdatePrefabsAndScenes();

            AssetDatabase.SaveAssets();
            Debug.Log("[AnimationIntegrator] Animation consolidation completed successfully!");
        }

        private static void IntegrateCharacterController()
        {
            string ctrlPath = "Assets/Animation/Main_Character/Animator Controller Character.controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null)
            {
                Debug.LogError("[AnimationIntegrator] Character controller not found at: " + ctrlPath);
                return;
            }

            // Ensure all needed parameters exist
            void EnsureParam(string name, AnimatorControllerParameterType type)
            {
                foreach (var p in ctrl.parameters)
                {
                    if (p.name == name) return;
                }
                ctrl.AddParameter(name, type);
            }

            EnsureParam("IsWalking", AnimatorControllerParameterType.Bool);
            EnsureParam("IsShooting", AnimatorControllerParameterType.Bool);
            EnsureParam("IsDashing", AnimatorControllerParameterType.Bool);
            EnsureParam("IsJumping", AnimatorControllerParameterType.Bool);
            EnsureParam("IsDashingBack", AnimatorControllerParameterType.Bool);
            EnsureParam("IsJumpShooting", AnimatorControllerParameterType.Bool);
            EnsureParam("IsDashingBackShoot", AnimatorControllerParameterType.Bool);
            EnsureParam("IsDashingJumpShoot", AnimatorControllerParameterType.Bool);
            EnsureParam("IsDashingShoot", AnimatorControllerParameterType.Bool);
            EnsureParam("IsDashingBackJumpShoot", AnimatorControllerParameterType.Bool);

            // Backward-compatible parameters
            EnsureParam("Speed", AnimatorControllerParameterType.Float);
            EnsureParam("IsGrounded", AnimatorControllerParameterType.Bool);
            EnsureParam("Dash", AnimatorControllerParameterType.Trigger);
            EnsureParam("DashBack", AnimatorControllerParameterType.Trigger);
            EnsureParam("Jump", AnimatorControllerParameterType.Trigger);
            EnsureParam("Shoot", AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;

            // Load all 11 character clips
            string dir = "Assets/Animation/Main_Character/";
            var clipIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "idle.anim");
            var clipWalk = AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "Wolk.anim") 
                           ?? AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "Walk.anim");
            var clipJump = AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "Jump.anim");
            var clipShoot = AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "Shoot.anim");
            var clipDash = AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "Dash.anim");
            var clipDashBack = AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "Dash(ถอยหลัง).anim");
            var clipJumpShoot = AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "JumpShoot.anim");
            var clipDashShoot = AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "Dash(พุ่งยิง).anim");
            var clipDashBackShoot = AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "Dash(ถอยหลังยิง).anim");
            var clipDashBackJumpShoot = AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "Dash(ถอยหลังกระโดดยิง).anim");
            var clipDashJumpShoot = AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "Dash(ไปข้างหน้ากระโดดยิง).anim");

            // Helper to get or create state
            AnimatorState GetOrCreateState(string name, Motion motion, Vector3 pos)
            {
                foreach (var cs in sm.states)
                {
                    if (cs.state.name == name)
                    {
                        if (motion != null) cs.state.motion = motion;
                        return cs.state;
                    }
                }
                var newState = sm.AddState(name, pos);
                newState.motion = motion;
                return newState;
            }

            var stIdle = GetOrCreateState("Idle", clipIdle, new Vector3(-600, -260, 0));
            var stWalk = GetOrCreateState("Walk", clipWalk, new Vector3(-350, 60, 0));
            var stJump = GetOrCreateState("Jump", clipJump, new Vector3(520, -200, 0));
            var stShoot = GetOrCreateState("Shoot", clipShoot, new Vector3(1110, 10, 0));
            var stDash = GetOrCreateState("Dash", clipDash, new Vector3(230, -440, 0));
            var stDashBack = GetOrCreateState("DashBack", clipDashBack, new Vector3(-180, -670, 0));
            var stJumpShoot = GetOrCreateState("JumpShoot", clipJumpShoot, new Vector3(1060, -410, 0));
            var stDashShoot = GetOrCreateState("DashShoot", clipDashShoot, new Vector3(90, -630, 0));
            var stDashBackShoot = GetOrCreateState("DashBackShoot", clipDashBackShoot, new Vector3(-420, -790, 0));
            var stDashBackJumpShoot = GetOrCreateState("DashBackJumpShoot", clipDashBackJumpShoot, new Vector3(910, -660, 0));
            var stDashJumpShoot = GetOrCreateState("DashJumpShootFwd", clipDashJumpShoot, new Vector3(1290, -600, 0));

            sm.defaultState = stIdle;

            // Remove all existing transitions cleanly using Unity Editor API
            while (sm.anyStateTransitions.Length > 0)
            {
                sm.RemoveAnyStateTransition(sm.anyStateTransitions[0]);
            }

            foreach (var cs in sm.states)
            {
                while (cs.state.transitions.Length > 0)
                {
                    cs.state.RemoveTransition(cs.state.transitions[0]);
                }
            }

            // 1. Idle <-> Walk
            var tIdleToWalk = stIdle.AddTransition(stWalk);
            tIdleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
            tIdleToWalk.hasExitTime = false;
            tIdleToWalk.duration = 0.05f;

            var tWalkToIdle = stWalk.AddTransition(stIdle);
            tWalkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
            tWalkToIdle.hasExitTime = false;
            tWalkToIdle.duration = 0.05f;

            // 2. AnyState -> Jump (Mid-air locomotion, blocked by actions)
            var tAnyToJump = sm.AddAnyStateTransition(stJump);
            tAnyToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
            tAnyToJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsShooting");
            tAnyToJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumpShooting");
            tAnyToJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing");
            tAnyToJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingBack");
            tAnyToJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingShoot");
            tAnyToJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingBackShoot");
            tAnyToJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingJumpShoot");
            tAnyToJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingBackJumpShoot");
            tAnyToJump.hasExitTime = false;
            tAnyToJump.duration = 0.05f;
            tAnyToJump.canTransitionToSelf = false;

            var tJumpToIdle = stJump.AddTransition(stIdle);
            tJumpToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            tJumpToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
            tJumpToIdle.hasExitTime = false;
            tJumpToIdle.duration = 0.05f;

            var tJumpToWalk = stJump.AddTransition(stWalk);
            tJumpToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            tJumpToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
            tJumpToWalk.hasExitTime = false;
            tJumpToWalk.duration = 0.05f;

            // 3. AnyState -> Dash (Forward)
            var tAnyToDash = sm.AddAnyStateTransition(stDash);
            tAnyToDash.AddCondition(AnimatorConditionMode.If, 0, "IsDashing");
            tAnyToDash.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingShoot");
            tAnyToDash.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingJumpShoot");
            tAnyToDash.hasExitTime = false;
            tAnyToDash.duration = 0.02f;
            tAnyToDash.canTransitionToSelf = false;

            var tDashToIdle = stDash.AddTransition(stIdle);
            tDashToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing");
            tDashToIdle.hasExitTime = false;
            tDashToIdle.duration = 0.05f;

            var tDashExit = stDash.AddTransition(stIdle);
            tDashExit.hasExitTime = true;
            tDashExit.exitTime = 1f;
            tDashExit.duration = 0.05f;

            // 4. AnyState -> Dash(ถอยหลัง) (Backward)
            var tAnyToDashBack = sm.AddAnyStateTransition(stDashBack);
            tAnyToDashBack.AddCondition(AnimatorConditionMode.If, 0, "IsDashingBack");
            tAnyToDashBack.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingBackShoot");
            tAnyToDashBack.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingBackJumpShoot");
            tAnyToDashBack.hasExitTime = false;
            tAnyToDashBack.duration = 0.02f;
            tAnyToDashBack.canTransitionToSelf = false;

            var tDashBackToIdle = stDashBack.AddTransition(stIdle);
            tDashBackToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingBack");
            tDashBackToIdle.hasExitTime = false;
            tDashBackToIdle.duration = 0.05f;

            var tDashBackExit = stDashBack.AddTransition(stIdle);
            tDashBackExit.hasExitTime = true;
            tDashBackExit.exitTime = 1f;
            tDashBackExit.duration = 0.05f;

            // 5. AnyState -> Shoot (Ground Shoot only)
            var tAnyToShoot = sm.AddAnyStateTransition(stShoot);
            tAnyToShoot.AddCondition(AnimatorConditionMode.If, 0, "IsShooting");
            tAnyToShoot.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            tAnyToShoot.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing");
            tAnyToShoot.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingBack");
            tAnyToShoot.hasExitTime = false;
            tAnyToShoot.duration = 0.02f;
            tAnyToShoot.canTransitionToSelf = false;

            var tShootToIdle = stShoot.AddTransition(stIdle);
            tShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsShooting");
            tShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
            tShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            tShootToIdle.hasExitTime = false;
            tShootToIdle.duration = 0.05f;

            var tShootToWalk = stShoot.AddTransition(stWalk);
            tShootToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsShooting");
            tShootToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
            tShootToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            tShootToWalk.hasExitTime = false;
            tShootToWalk.duration = 0.05f;

            var tShootToJump = stShoot.AddTransition(stJump);
            tShootToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
            tShootToJump.hasExitTime = false;
            tShootToJump.duration = 0.05f;

            // 6. AnyState -> JumpShoot (Air Shoot)
            var tAnyToJumpShoot = sm.AddAnyStateTransition(stJumpShoot);
            tAnyToJumpShoot.AddCondition(AnimatorConditionMode.If, 0, "IsJumpShooting");
            tAnyToJumpShoot.hasExitTime = false;
            tAnyToJumpShoot.duration = 0.02f;
            tAnyToJumpShoot.canTransitionToSelf = false;

            var tJumpShootToJump = stJumpShoot.AddTransition(stJump);
            tJumpShootToJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumpShooting");
            tJumpShootToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
            tJumpShootToJump.hasExitTime = false;
            tJumpShootToJump.duration = 0.05f;

            var tJumpShootToIdle = stJumpShoot.AddTransition(stIdle);
            tJumpShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumpShooting");
            tJumpShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            tJumpShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
            tJumpShootToIdle.hasExitTime = false;
            tJumpShootToIdle.duration = 0.05f;

            var tJumpShootToWalk = stJumpShoot.AddTransition(stWalk);
            tJumpShootToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumpShooting");
            tJumpShootToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            tJumpShootToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
            tJumpShootToWalk.hasExitTime = false;
            tJumpShootToWalk.duration = 0.05f;

            // 7. AnyState -> Dash(พุ่งยิง)
            var tAnyToDashShoot = sm.AddAnyStateTransition(stDashShoot);
            tAnyToDashShoot.AddCondition(AnimatorConditionMode.If, 0, "IsDashingShoot");
            tAnyToDashShoot.hasExitTime = false;
            tAnyToDashShoot.duration = 0.02f;
            tAnyToDashShoot.canTransitionToSelf = false;

            var tDashShootToIdle = stDashShoot.AddTransition(stIdle);
            tDashShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingShoot");
            tDashShootToIdle.hasExitTime = false;
            tDashShootToIdle.duration = 0.05f;

            // 8. AnyState -> Dash(ถอยหลังยิง)
            var tAnyToDashBackShoot = sm.AddAnyStateTransition(stDashBackShoot);
            tAnyToDashBackShoot.AddCondition(AnimatorConditionMode.If, 0, "IsDashingBackShoot");
            tAnyToDashBackShoot.hasExitTime = false;
            tAnyToDashBackShoot.duration = 0.02f;
            tAnyToDashBackShoot.canTransitionToSelf = false;

            var tDashBackShootToIdle = stDashBackShoot.AddTransition(stIdle);
            tDashBackShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingBackShoot");
            tDashBackShootToIdle.hasExitTime = false;
            tDashBackShootToIdle.duration = 0.05f;

            // 9. AnyState -> Dash(ไปข้างหน้ากระโดดยิง)
            var tAnyToDashJumpShoot = sm.AddAnyStateTransition(stDashJumpShoot);
            tAnyToDashJumpShoot.AddCondition(AnimatorConditionMode.If, 0, "IsDashingJumpShoot");
            tAnyToDashJumpShoot.hasExitTime = false;
            tAnyToDashJumpShoot.duration = 0.02f;
            tAnyToDashJumpShoot.canTransitionToSelf = false;

            var tDashJumpShootToJump = stDashJumpShoot.AddTransition(stJump);
            tDashJumpShootToJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingJumpShoot");
            tDashJumpShootToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
            tDashJumpShootToJump.hasExitTime = false;
            tDashJumpShootToJump.duration = 0.05f;

            var tDashJumpShootToIdle = stDashJumpShoot.AddTransition(stIdle);
            tDashJumpShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingJumpShoot");
            tDashJumpShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            tDashJumpShootToIdle.hasExitTime = false;
            tDashJumpShootToIdle.duration = 0.05f;

            // 10. AnyState -> Dash(ถอยหลังกระโดดยิง)
            var tAnyToDashBackJumpShoot = sm.AddAnyStateTransition(stDashBackJumpShoot);
            tAnyToDashBackJumpShoot.AddCondition(AnimatorConditionMode.If, 0, "IsDashingBackJumpShoot");
            tAnyToDashBackJumpShoot.hasExitTime = false;
            tAnyToDashBackJumpShoot.duration = 0.02f;
            tAnyToDashBackJumpShoot.canTransitionToSelf = false;

            var tDashBackJumpShootToJump = stDashBackJumpShoot.AddTransition(stJump);
            tDashBackJumpShootToJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingBackJumpShoot");
            tDashBackJumpShootToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
            tDashBackJumpShootToJump.hasExitTime = false;
            tDashBackJumpShootToJump.duration = 0.05f;

            var tDashBackJumpShootToIdle = stDashBackJumpShoot.AddTransition(stIdle);
            tDashBackJumpShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashingBackJumpShoot");
            tDashBackJumpShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
            tDashBackJumpShootToIdle.hasExitTime = false;
            tDashBackJumpShootToIdle.duration = 0.05f;

            EditorUtility.SetDirty(ctrl);
            Debug.Log("[AnimationIntegrator] Configured Animator Controller Character with all 11 clips and clean transitions.");
        }

        private static void IntegrateMonsterController()
        {
            string ctrlPath = "Assets/Animation/Monster/มอนตีใกล้(Animator Controller).controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null)
            {
                Debug.LogError("[AnimationIntegrator] Monster controller not found at: " + ctrlPath);
                return;
            }

            void EnsureParam(string name, AnimatorControllerParameterType type)
            {
                foreach (var p in ctrl.parameters)
                {
                    if (p.name == name) return;
                }
                ctrl.AddParameter(name, type);
            }

            EnsureParam("IsAttacking", AnimatorControllerParameterType.Bool);
            EnsureParam("IsWalking", AnimatorControllerParameterType.Bool);
            EnsureParam("Speed", AnimatorControllerParameterType.Float);
            EnsureParam("Attack", AnimatorControllerParameterType.Trigger);
            EnsureParam("Shoot", AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;

            string mDir = "Assets/Animation/Monster/";
            var clipIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(mDir + "idleEnemy.anim");
            var clipWalk = AssetDatabase.LoadAssetAtPath<AnimationClip>(mDir + "EnemyWolk.anim") 
                           ?? AssetDatabase.LoadAssetAtPath<AnimationClip>(mDir + "WolkEnemy(ตีใกล้).anim");
            var clipAtk = AssetDatabase.LoadAssetAtPath<AnimationClip>(mDir + "EnemyAttack(ตีใกล้).anim");
            var clipShoot = AssetDatabase.LoadAssetAtPath<AnimationClip>(mDir + "EnemyShoot.anim");

            AnimatorState GetOrCreateState(string name, Motion motion, Vector3 pos)
            {
                foreach (var cs in sm.states)
                {
                    if (cs.state.name == name)
                    {
                        if (motion != null) cs.state.motion = motion;
                        return cs.state;
                    }
                }
                var newState = sm.AddState(name, pos);
                newState.motion = motion;
                return newState;
            }

            var stIdle = GetOrCreateState("idleEnemy", clipIdle, new Vector3(300, 50, 0));
            var stWalk = GetOrCreateState("Walk", clipWalk, new Vector3(300, 180, 0));
            var stAtk = GetOrCreateState("EnemyAttack", clipAtk, new Vector3(650, 0, 0));
            var stShoot = GetOrCreateState("EnemyShoot", clipShoot, new Vector3(650, 150, 0));

            sm.defaultState = stIdle;

            while (sm.anyStateTransitions.Length > 0)
            {
                sm.RemoveAnyStateTransition(sm.anyStateTransitions[0]);
            }

            foreach (var cs in sm.states)
            {
                while (cs.state.transitions.Length > 0)
                {
                    cs.state.RemoveTransition(cs.state.transitions[0]);
                }
            }

            // Idle <-> Walk
            var tIdleToWalk = stIdle.AddTransition(stWalk);
            tIdleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
            tIdleToWalk.hasExitTime = false;
            tIdleToWalk.duration = 0.05f;

            var tWalkToIdle = stWalk.AddTransition(stIdle);
            tWalkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
            tWalkToIdle.hasExitTime = false;
            tWalkToIdle.duration = 0.05f;

            // AnyState -> Attack
            var tAnyToAtk = sm.AddAnyStateTransition(stAtk);
            tAnyToAtk.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");
            tAnyToAtk.hasExitTime = false;
            tAnyToAtk.duration = 0.02f;
            tAnyToAtk.canTransitionToSelf = false;

            var tAtkExit = stAtk.AddTransition(stIdle);
            tAtkExit.hasExitTime = true;
            tAtkExit.exitTime = 0.9f;
            tAtkExit.duration = 0.05f;

            // AnyState -> Shoot
            if (clipShoot != null)
            {
                var tAnyToShoot = sm.AddAnyStateTransition(stShoot);
                tAnyToShoot.AddCondition(AnimatorConditionMode.If, 0, "Shoot");
                tAnyToShoot.hasExitTime = false;
                tAnyToShoot.duration = 0.02f;
                tAnyToShoot.canTransitionToSelf = false;

                var tShootExit = stShoot.AddTransition(stIdle);
                tShootExit.hasExitTime = true;
                tShootExit.exitTime = 0.9f;
                tShootExit.duration = 0.05f;
            }

            EditorUtility.SetDirty(ctrl);
            Debug.Log("[AnimationIntegrator] Configured Monster controller with Idle, Walk, Attack, Shoot.");
        }

        private static void UpdatePrefabsAndScenes()
        {
            var charCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animation/Main_Character/Animator Controller Character.controller");
            var monsterCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animation/Monster/มอนตีใกล้(Animator Controller).controller");

            var playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Animation/Main_Character/หันขวา.png");
            var enemySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Animation/Monster/มอนตีใกล้2.png");

            // 1. Update Player.prefab
            string playerPrefabPath = "Assets/Prefabs/Player.prefab";
            using (var scope = new PrefabUtility.EditPrefabContentsScope(playerPrefabPath))
            {
                var root = scope.prefabContentsRoot;
                var anim = root.GetComponent<Animator>();
                if (anim == null) anim = root.AddComponent<Animator>();
                anim.runtimeAnimatorController = charCtrl;

                var sr = root.GetComponent<SpriteRenderer>();
                if (sr != null && playerSprite != null && (sr.sprite == null || sr.sprite.name == "Capsule"))
                {
                    sr.sprite = playerSprite;
                }
            }

            // 2. Update Enemy prefabs
            string[] enemyPrefabs = new[] { "Assets/Prefabs/Enemy_Melee.prefab", "Assets/Prefabs/Enemy_Shooter.prefab" };
            foreach (var ep in enemyPrefabs)
            {
                using (var scope = new PrefabUtility.EditPrefabContentsScope(ep))
                {
                    var root = scope.prefabContentsRoot;
                    var anim = root.GetComponent<Animator>();
                    if (anim == null) anim = root.AddComponent<Animator>();
                    anim.runtimeAnimatorController = monsterCtrl;

                    var sr = root.GetComponent<SpriteRenderer>();
                    if (sr != null && enemySprite != null && (sr.sprite == null || sr.sprite.name == "Capsule"))
                    {
                        sr.sprite = enemySprite;
                    }
                }
            }

            // 3. Update active scene objects
            var players = Object.FindObjectsByType<CaveDweller.Player.PlayerMovement>();
            foreach (var p in players)
            {
                var anim = p.GetComponent<Animator>();
                if (anim == null) anim = p.gameObject.AddComponent<Animator>();
                anim.runtimeAnimatorController = charCtrl;

                var sr = p.GetComponent<SpriteRenderer>();
                if (sr != null && playerSprite != null && (sr.sprite == null || sr.sprite.name == "Capsule"))
                {
                    sr.sprite = playerSprite;
                }
                EditorUtility.SetDirty(p.gameObject);
            }

            var enemies = Object.FindObjectsByType<CaveDweller.Enemies.BaseEnemy>();
            foreach (var e in enemies)
            {
                var anim = e.GetComponent<Animator>();
                if (anim == null) anim = e.gameObject.AddComponent<Animator>();
                anim.runtimeAnimatorController = monsterCtrl;

                var sr = e.GetComponent<SpriteRenderer>();
                if (sr != null && enemySprite != null && (sr.sprite == null || sr.sprite.name == "Capsule"))
                {
                    sr.sprite = enemySprite;
                }
                EditorUtility.SetDirty(e.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
    }
}
