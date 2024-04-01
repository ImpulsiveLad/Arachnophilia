using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Netcode;
using UnityEngine;

namespace Arachnophilia.Patches
{
    [HarmonyPatch(typeof(SandSpiderAI), "Start")]
    internal class SandSpiderStartPatch
    {
        static void Postfix(SandSpiderAI __instance)
        {
            __instance.maxWebTrapsToPlace = Random.Range(SyncConfig.Instance.MinWebCount.Value - 1 - SyncConfig.Instance.MinSpooledBodies.Value * 1 / 3, SyncConfig.Instance.MaxWebCount.Value - SyncConfig.Instance.MaxSpooledBodies.Value * 1 / 2);
            __instance.enemyHP = SyncConfig.Instance.SpiderHp.Value;
        }
    }
    [HarmonyPatch(typeof(SandSpiderAI), "AttemptPlaceWebTrap")]
    internal class SandSpiderAttemptPlaceWebTrapPatch : GrabbableObject
    {
        static bool Prefix(SandSpiderAI __instance)
        {
            for (int i = 0; i < __instance.webTraps.Count; i++)
            {
                if (Vector3.Distance(__instance.webTraps[i].transform.position, __instance.abdomen.position) < SyncConfig.Instance.MinWebDistance.Value)
                {
                    return false;
                }
            }
            float x = Random.Range(-1f, 1f);
            float z = Random.Range(-1f, 1f);
            float y = Random.Range(0, 1f);
            Vector3 direction = new Vector3(x, y, z).normalized;
            Ray ray = new Ray(__instance.abdomen.position + Vector3.up * 0.4f, direction);
            RaycastHit rayHit;
            if (Physics.Raycast(ray, out rayHit, SyncConfig.Instance.MaxWebLength.Value, StartOfRound.Instance.collidersAndRoomMask))
            {
                if (rayHit.distance < SyncConfig.Instance.MinWebLength.Value)
                {
                    return false;
                }
                Arachnophilia.instance.mls.LogInfo($"Current time: {System.DateTime.Now} - Got spider web raycast; end point: {rayHit.point}; {rayHit.distance}");
                Vector3 point = rayHit.point;
                if (Physics.Raycast(__instance.abdomen.position, Vector3.down, out rayHit, 10f, StartOfRound.Instance.collidersAndRoomMask))
                {
                    Vector3 startPosition = rayHit.point + Vector3.up * 0.2f;
                    __instance.SpawnWebTrapServerRpc(startPosition, point);
                }
            }
            return false;
        }
    }
    internal class SandSpiderState
    {
        public Dictionary<SandSpiderAI, int> bodyCounts = new Dictionary<SandSpiderAI, int>();
        public Dictionary<SandSpiderAI, bool> coroutineStartedFlags = new Dictionary<SandSpiderAI, bool>();
        public Dictionary<SandSpiderAI, int> randomSpooledBodies = new Dictionary<SandSpiderAI, int>();
    }
    [HarmonyPatch(typeof(SandSpiderAI), "Update")]
    internal class SandSpiderUpdatePatch
    {
        static SandSpiderState state = new SandSpiderState();
        static void Postfix(SandSpiderAI __instance)
        {
            lock (state.bodyCounts)
            {
                lock (state.coroutineStartedFlags)
                {
                    if (!state.bodyCounts.ContainsKey(__instance))
                    {
                        state.bodyCounts[__instance] = 0;
                        state.coroutineStartedFlags[__instance] = false;
                        state.randomSpooledBodies[__instance] = Random.Range(SyncConfig.Instance.MinSpooledBodies.Value, SyncConfig.Instance.MaxSpooledBodies.Value + 1);
                    }
                }
            }
            int randomSpooledBodies = state.randomSpooledBodies[__instance];
            float webSettingSpeedMultiplier = SyncConfig.Instance.SpiderSetupSpeed.Value;
            float playerChasingSpeedMultiplier = SyncConfig.Instance.SpiderChaseSpeed.Value;
            switch (__instance.currentBehaviourStateIndex)
            {
                case 0:
                    __instance.agent.speed *= webSettingSpeedMultiplier;
                    __instance.spiderSpeed *= webSettingSpeedMultiplier;
                    lock (state.coroutineStartedFlags)
                    {
                        state.coroutineStartedFlags[__instance] = false;
                    }
                    break;
                case 1:
                    __instance.agent.speed *= webSettingSpeedMultiplier;
                    __instance.spiderSpeed *= webSettingSpeedMultiplier;
                    lock (state.bodyCounts)
                    {
                        lock (state.coroutineStartedFlags)
                        {
                            if (state.bodyCounts[__instance] < randomSpooledBodies && !state.coroutineStartedFlags[__instance])
                            {
                                __instance.StartCoroutine(InstantiateRagdoll(__instance));
                                state.coroutineStartedFlags[__instance] = true;
                            }
                        }
                    }
                    break;
                case 2:
                    __instance.agent.speed *= playerChasingSpeedMultiplier;
                    __instance.spiderSpeed *= playerChasingSpeedMultiplier;
                    if (__instance.currentlyHeldBody != null)
                    {
                        __instance.CancelSpoolingBody();
                    }
                    lock (state.coroutineStartedFlags)
                    {
                        state.coroutineStartedFlags[__instance] = false;
                    }
                    break;
            }
        }
        static IEnumerator InstantiateRagdoll(SandSpiderAI __instance)
        {
            while (__instance.isOutside)
            {
                yield return null;
            }
            while (__instance.currentBehaviourStateIndex == 2)
            {
                yield return null;
            }
            while (__instance.currentlyHeldBody != null)
            {
                yield return null;
            }
            yield return new WaitForSeconds(3f);
            if (__instance.isEnemyDead)
            {
                yield break;
            }
            GameObject ragdollPrefab = StartOfRound.Instance.playerRagdolls[0];
            GameObject ragdoll = Object.Instantiate(ragdollPrefab, __instance.homeNode.position, Quaternion.identity);
            DeadBodyInfo deadBodyInfo = ragdoll.GetComponent<DeadBodyInfo>();
            ragdoll.AddComponent<NetworkObject>();
            Transform bodyScanNode = ragdoll.transform.Find("ScanNode");
            if (bodyScanNode != null)
            {
                bodyScanNode.gameObject.SetActive(false);
            }
            Transform bodyMapDot = ragdoll.transform.Find("MapDot");
            if (bodyMapDot != null)
            {
                bodyMapDot.gameObject.SetActive(false);
            }
            BoxCollider bodyBoxCollider = ragdoll.GetComponent<BoxCollider>();
            if (bodyBoxCollider != null)
            {
                bodyBoxCollider.enabled = false;
            }
            __instance.spooledPlayerBody = false;
            __instance.decidedChanceToHangBodyEarly = false;
            __instance.turnBodyIntoWebCoroutine = null;
            if (__instance.currentlyHeldBody != null)
            {
                __instance.SpiderTurnBodyIntoWebServerRpc();
            }
            if (__instance.currentlyHeldBody != null)
            {
                __instance.SpiderHangBodyServerRpc();
            }
            __instance.GrabBody(deadBodyInfo);
            Arachnophilia.instance.mls.LogInfo($"Current time: {System.DateTime.Now} - Spoolable prey instantiated");
            lock (state.bodyCounts)
            {
                lock (state.coroutineStartedFlags)
                {
                    state.bodyCounts[__instance]++;
                    state.coroutineStartedFlags[__instance] = false;
                }
            }
            if (!__instance.spoolingPlayerBody)
            {
                __instance.currentBehaviourStateIndex = 0;
            }
        }
    }
    [HarmonyPatch(typeof(SandSpiderAI), "Update")]
    public static class SandSpiderUpdateTPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_R4)
                {
                    float operandValue = (float)codes[i].operand;

                    if (operandValue == 4f)
                    {
                        codes[i].operand = SyncConfig.Instance.WebPlaceInterval.Value;
                    }
                    else if (operandValue == 0.5f)
                    {
                        codes[i].operand = SyncConfig.Instance.WebTimeRange.Value / 2;
                    }
                    else if (operandValue == 1f)
                    {
                        codes[i].operand = SyncConfig.Instance.WebTimeRange.Value;
                    }
                    else if (operandValue == 0.17f)
                    {
                        codes[i].operand = SyncConfig.Instance.FailedWebTrapTime.Value;
                    }
                }
            }
            return codes;
        }
    }
    [HarmonyPatch(typeof(SandSpiderAI), "OnCollideWithPlayer")]
    public static class SandSpiderOnCollideWithPlayerPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_S && (sbyte)codes[i].operand == 90)
                {
                    codes[i].operand = (sbyte)SyncConfig.Instance.SpiderDamage.Value;
                }
            }
            return codes.AsEnumerable();
        }
    }
    [HarmonyPatch(typeof(SandSpiderAI), "GetWallPositionForSpiderMesh")]
    public static class SandSpiderGetWallPositionForSpiderMeshPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_R4)
                {
                    float operandValue = (float)codes[i].operand;

                    if (operandValue == 6f)
                    {
                        codes[i].operand = SyncConfig.Instance.MinWallHeight.Value;
                    }
                    else if (operandValue == 22f)
                    {
                        codes[i].operand = SyncConfig.Instance.MaxWallHeight.Value;
                    }
                    else if (operandValue == 10f)
                    {
                        codes[i].operand = 100f;
                    }
                    else if (operandValue == 10.1f)
                    {
                        codes[i].operand = 100.1f;
                    }
                    else if (operandValue == 7f)
                    {
                        codes[i].operand = SyncConfig.Instance.FloorCheck.Value;
                    }
                }
            }
            return codes.AsEnumerable();
        }
    }
    [HarmonyPatch(typeof(SandSpiderAI), "GrabBody")]
    public static class SandSpiderAIGrabBodyPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(SandSpiderAI), "GrabBody"))
                {
                    codes.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SandSpiderAIGrabBodyPatch), "ModifiedGrabBody")));
                    break;
                }
            }
            return codes.AsEnumerable();
        }
        public static void ModifiedGrabBody(SandSpiderAI __instance, DeadBodyInfo body)
        {
            if (!__instance.isOutside)
            {
                __instance.currentlyHeldBody = body;
                __instance.currentlyHeldBody.attachedLimb = __instance.currentlyHeldBody.bodyParts[6];
                __instance.currentlyHeldBody.attachedTo = __instance.mouthTarget;
                __instance.currentlyHeldBody.matchPositionExactly = true;
            }
        }
    }
}
