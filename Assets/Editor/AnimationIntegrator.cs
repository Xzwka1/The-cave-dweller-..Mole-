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
        [InitializeOnLoadMethod]
        public static void RunIntegration()
        {
            EditorApplication.delayCall += Execute;
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
            EnsureParam("IsDashing(ถอยหลัง)", AnimatorControllerParameterType.Bool);
            EnsureParam("IsJumpShooting", AnimatorControllerParameterType.Bool);
            EnsureParam("IsDashing(ถอยหลังยิง)", AnimatorControllerParameterType.Bool);
            EnsureParam("IsDashing(กระโดดยิง)", AnimatorControllerParameterType.Bool);
            EnsureParam("IsDashingShoot(พุ่งยิง)", AnimatorControllerParameterType.Bool);
            EnsureParam("IsDashingShoot(พุ่งถอยหลังยิง)", AnimatorControllerParameterType.Bool);

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
            var clipWalk = AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + "Walk.anim");
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
            var stDashBack = GetOrCreateState("Dash(ถอยหลัง)", clipDashBack, new Vector3(-180, -670, 0));
            var stJumpShoot = GetOrCreateState("JumpShoot", clipJumpShoot, new Vector3(1060, -410, 0));
            var stDashShoot = GetOrCreateState("Dash(พุ่งยิง)", clipDashShoot, new Vector3(90, -630, 0));
            var stDashBackShoot = GetOrCreateState("Dash(ถอยหลังยิง)", clipDashBackShoot, new Vector3(-420, -790, 0));
            var stDashBackJumpShoot = GetOrCreateState("Dash(ถอยหลังกระโดดยิง)", clipDashBackJumpShoot, new Vector3(910, -660, 0));
            var stDashJumpShoot = GetOrCreateState("Dash(ไปข้างหน้ากระโดดยิง)", clipDashJumpShoot, new Vector3(1290, -600, 0));

            sm.defaultState = stIdle;

            // Remove all transitions to rebuild clean, bug-free, snappy transitions
            stIdle.transitions = new AnimatorStateTransition[0];
            stWalk.transitions = new AnimatorStateTransition[0];
            stJump.transitions = new AnimatorStateTransition[0];
            stShoot.transitions = new AnimatorStateTransition[0];
            stDash.transitions = new AnimatorStateTransition[0];
            stDashBack.transitions = new AnimatorStateTransition[0];
            stJumpShoot.transitions = new AnimatorStateTransition[0];
            stDashShoot.transitions = new AnimatorStateTransition[0];
            stDashBackShoot.transitions = new AnimatorStateTransition[0];
            stDashBackJumpShoot.transitions = new AnimatorStateTransition[0];
            stDashJumpShoot.transitions = new AnimatorStateTransition[0];
            sm.anyStateTransitions = new AnimatorStateTransition[0];

            // 1. Idle <-> Walk
            var tIdleToWalk = stIdle.AddTransition(stWalk);
            tIdleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
            tIdleToWalk.hasExitTime = false;
            tIdleToWalk.duration = 0.05f;

            var tWalkToIdle = stWalk.AddTransition(stIdle);
            tWalkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
            tWalkToIdle.hasExitTime = false;
            tWalkToIdle.duration = 0.05f;

            // 2. AnyState -> Jump
            var tAnyToJump = sm.AddAnyStateTransition(stJump);
            tAnyToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
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
            tAnyToDashBack.AddCondition(AnimatorConditionMode.If, 0, "IsDashing(ถอยหลัง)");
            tAnyToDashBack.hasExitTime = false;
            tAnyToDashBack.duration = 0.02f;
            tAnyToDashBack.canTransitionToSelf = false;

            var tDashBackToIdle = stDashBack.AddTransition(stIdle);
            tDashBackToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing(ถอยหลัง)");
            tDashBackToIdle.hasExitTime = false;
            tDashBackToIdle.duration = 0.05f;

            var tDashBackExit = stDashBack.AddTransition(stIdle);
            tDashBackExit.hasExitTime = true;
            tDashBackExit.exitTime = 1f;
            tDashBackExit.duration = 0.05f;

            // 5. AnyState -> Shoot
            var tAnyToShoot = sm.AddAnyStateTransition(stShoot);
            tAnyToShoot.AddCondition(AnimatorConditionMode.If, 0, "IsShooting");
            tAnyToShoot.hasExitTime = false;
            tAnyToShoot.duration = 0.02f;
            tAnyToShoot.canTransitionToSelf = false;

            var tShootToIdle = stShoot.AddTransition(stIdle);
            tShootToIdle.hasExitTime = true;
            tShootToIdle.exitTime = 0.9f;
            tShootToIdle.duration = 0.05f;

            // 6. AnyState -> JumpShoot
            var tAnyToJumpShoot = sm.AddAnyStateTransition(stJumpShoot);
            tAnyToJumpShoot.AddCondition(AnimatorConditionMode.If, 0, "IsJumpShooting");
            tAnyToJumpShoot.hasExitTime = false;
            tAnyToJumpShoot.duration = 0.02f;
            tAnyToJumpShoot.canTransitionToSelf = false;

            var tJumpShootExit = stJumpShoot.AddTransition(stJump);
            tJumpShootExit.hasExitTime = true;
            tJumpShootExit.exitTime = 0.9f;
            tJumpShootExit.duration = 0.05f;

            // 7. AnyState -> Dash(พุ่งยิง)
            var tAnyToDashShoot = sm.AddAnyStateTransition(stDashShoot);
            tAnyToDashShoot.AddCondition(AnimatorConditionMode.If, 0, "IsDashingShoot(พุ่งยิง)");
            tAnyToDashShoot.hasExitTime = false;
            tAnyToDashShoot.duration = 0.02f;
            tAnyToDashShoot.canTransitionToSelf = false;

            var tDashShootExit = stDashShoot.AddTransition(stIdle);
            tDashShootExit.hasExitTime = true;
            tDashShootExit.exitTime = 0.9f;
            tDashShootExit.duration = 0.05f;

            // 8. AnyState -> Dash(ถอยหลังยิง)
            var tAnyToDashBackShoot = sm.AddAnyStateTransition(stDashBackShoot);
            tAnyToDashBackShoot.AddCondition(AnimatorConditionMode.If, 0, "IsDashing(ถอยหลังยิง)");
            tAnyToDashBackShoot.hasExitTime = false;
            tAnyToDashBackShoot.duration = 0.02f;
            tAnyToDashBackShoot.canTransitionToSelf = false;

            var tDashBackShootExit = stDashBackShoot.AddTransition(stIdle);
            tDashBackShootExit.hasExitTime = true;
            tDashBackShootExit.exitTime = 0.9f;
            tDashBackShootExit.duration = 0.05f;

            // 9. AnyState -> Dash(ไปข้างหน้ากระโดดยิง)
            var tAnyToDashJumpShoot = sm.AddAnyStateTransition(stDashJumpShoot);
            tAnyToDashJumpShoot.AddCondition(AnimatorConditionMode.If, 0, "IsDashing(กระโดดยิง)");
            tAnyToDashJumpShoot.hasExitTime = false;
            tAnyToDashJumpShoot.duration = 0.02f;
            tAnyToDashJumpShoot.canTransitionToSelf = false;

            var tDashJumpShootExit = stDashJumpShoot.AddTransition(stIdle);
            tDashJumpShootExit.hasExitTime = true;
            tDashJumpShootExit.exitTime = 0.9f;
            tDashJumpShootExit.duration = 0.05f;

            // 10. AnyState -> Dash(ถอยหลังกระโดดยิง)
            var tAnyToDashBackJumpShoot = sm.AddAnyStateTransition(stDashBackJumpShoot);
            tAnyToDashBackJumpShoot.AddCondition(AnimatorConditionMode.If, 0, "IsDashingShoot(พุ่งถอยหลังยิง)");
            tAnyToDashBackJumpShoot.hasExitTime = false;
            tAnyToDashBackJumpShoot.duration = 0.02f;
            tAnyToDashBackJumpShoot.canTransitionToSelf = false;

            var tDashBackJumpShootExit = stDashBackJumpShoot.AddTransition(stIdle);
            tDashBackJumpShootExit.hasExitTime = true;
            tDashBackJumpShootExit.exitTime = 0.9f;
            tDashBackJumpShootExit.duration = 0.05f;

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
            var clipWalk = AssetDatabase.LoadAssetAtPath<AnimationClip>(mDir + "WolkEnemy(ตีใกล้).anim");
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

            stIdle.transitions = new AnimatorStateTransition[0];
            stWalk.transitions = new AnimatorStateTransition[0];
            stAtk.transitions = new AnimatorStateTransition[0];
            stShoot.transitions = new AnimatorStateTransition[0];
            sm.anyStateTransitions = new AnimatorStateTransition[0];

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
            string[] enemyPrefabs = new[] { "Assets/Prefabs/Enemy_Light.prefab", "Assets/Prefabs/Enemy_Dark.prefab" };
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
            var players = Object.FindObjectsByType<CaveDweller.Player.PlayerMovement>(FindObjectsSortMode.None);
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

            var enemies = Object.FindObjectsByType<CaveDweller.Enemies.BaseEnemy>(FindObjectsSortMode.None);
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
