using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ThienKhuyet.Characters;
using UnityEngine;

namespace ThienKhuyet.Tests
{
    public static class ProjectPaths
    {
        /// <summary>Unity project root: TK_PROJECT env var (headless runner) or the Editor's working directory.</summary>
        public static string Root
        {
            get
            {
                string env = System.Environment.GetEnvironmentVariable("TK_PROJECT");
                return string.IsNullOrEmpty(env) ? Directory.GetCurrentDirectory() : env;
            }
        }

        public static string Asset(string relative)
        {
            return Path.Combine(Root, "Assets", "ThienKhuyet", relative);
        }
    }

    public class PoseMathTests
    {
        static void AssertVec(Vector3 expected, Vector3 actual, string msg = null)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, msg + " x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, msg + " y");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, msg + " z");
        }

        [Test]
        public void EulerMatchesUnityConventions()
        {
            AssertVec(new Vector3(0, -1, 0), Q.EulerZXY(90, 0, 0) * Vector3.forward, "x+ dips forward vector down");
            AssertVec(new Vector3(1, 0, 0), Q.EulerZXY(0, 90, 0) * Vector3.forward, "y+ turns right");
            AssertVec(new Vector3(-1, 0, 0), Q.EulerZXY(0, 0, 90) * Vector3.up, "z+ tilts up toward -x");
        }

        [Test]
        public void EulerOrderIsZThenXThenY()
        {
            Quaternion expected = Q.EulerZXY(0, 40, 0) * Q.EulerZXY(30, 0, 0) * Q.EulerZXY(0, 0, 50);
            Quaternion actual = Q.EulerZXY(30, 40, 50);
            Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(expected, actual)), 1e-4f);
        }

        [Test]
        public void NlerpTakesShortestArc()
        {
            Quaternion a = Q.EulerZXY(0, 0, 0), b = Q.EulerZXY(0, 359, 0);
            Quaternion mid = Q.Nlerp(a, b, 0.5f);
            Vector3 f = mid * Vector3.forward;
            Assert.Greater(f.z, 0.99f, "blending 0 and 359 degrees must stay near 0, not swing through 180");
        }
    }

    public class PoseScriptTests
    {
        static Dictionary<string, PoseClip> Load(string file, RigSpec rig, out List<string> errors)
        {
            errors = new List<string>();
            string text = File.ReadAllText(ProjectPaths.Asset("Resources/Animation/" + file));
            return PoseParser.Parse(text, rig, file, errors);
        }

        static readonly string[] RequiredHumanoid =
        {
            "sword_l1", "sword_l2", "sword_l3", "sword_heavy", "fist_l1", "fist_l2", "fist_l3", "fist_heavy",
            "spear_l1", "spear_l2", "spear_heavy", "staff_l1", "staff_l2", "staff_l3", "staff_heavy",
            "guard_sword", "guard_fist", "guard_spear", "guard_staff", "parry", "hit_light", "hit_heavy", "stagger", "death",
            "dodge_roll", "jump_up", "fall", "land", "cast_forward", "cast_up", "cast_slam", "cast_buff", "cast_dash", "cast_slash", "cast_palm",
            "meditate", "sit_ground", "lie_back", "wake_up", "kneel", "gather", "gesture_a", "gesture_b", "point", "salute", "bow", "nod",
            "shake_head", "startled", "look_around", "reach_out", "fall_free", "stance_sword", "stance_fist", "stance_spear", "stance_staff"
        };

        static readonly string[] RequiredQuadruped = { "wolf_bite", "wolf_pounce", "wolf_howl", "hit_light", "hit_heavy", "stagger", "death", "alert", "sleep" };

        [Test]
        public void HumanoidScriptParsesWithoutErrors()
        {
            var clips = Load("humanoid_poses.txt", Rigs.Humanoid, out List<string> errors);
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
            foreach (string id in RequiredHumanoid) Assert.IsTrue(clips.ContainsKey(id), "missing humanoid clip " + id);
        }

        [Test]
        public void QuadrupedScriptParsesWithoutErrors()
        {
            var clips = Load("quadruped_poses.txt", Rigs.Quadruped, out List<string> errors);
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
            foreach (string id in RequiredQuadruped) Assert.IsTrue(clips.ContainsKey(id), "missing quadruped clip " + id);
        }

        [Test]
        public void ClipsAreWellFormed()
        {
            foreach (var file in new[] { "humanoid_poses.txt", "quadruped_poses.txt" })
            {
                var rig = file.StartsWith("humanoid") ? Rigs.Humanoid : Rigs.Quadruped;
                var clips = Load(file, rig, out _);
                foreach (var kv in clips)
                {
                    PoseClip c = kv.Value;
                    Assert.Greater(c.KeyCount, 0, kv.Key + " has no keys");
                    Assert.Greater(c.length, 0.05f, kv.Key + " length");
                    for (int i = 1; i < c.KeyCount; i++) Assert.GreaterOrEqual(c.times[i], c.times[i - 1], kv.Key + " key times must ascend");
                    bool any = false;
                    for (int j = 0; j < c.mask.Length; j++) any |= c.mask[j];
                    Assert.IsTrue(any, kv.Key + " controls no joint");
                }
            }
        }

        [Test]
        public void SamplingInterpolatesAndHolds()
        {
            var clips = Load("humanoid_poses.txt", Rigs.Humanoid, out _);
            PoseClip c = clips["nod"];
            var e = new Vector3[17];
            c.Sample(0.2f, e);
            Assert.AreEqual(18f, e[Rigs.Hd].x, 1e-3f);
            c.Sample(5f, e);
            Assert.AreEqual(0f, e[Rigs.Hd].x, 1e-3f);
            c.Sample(0.325f, e);
            Assert.Less(e[Rigs.Hd].x, 18f);
            Assert.Greater(e[Rigs.Hd].x, -4f);
        }

        [Test]
        public void LaterKeysInheritEarlierJointValues()
        {
            var clips = Load("humanoid_poses.txt", Rigs.Humanoid, out _);
            PoseClip c = clips["sword_l3"];
            // key at 0.52 only names ArR/FoR/Pos but must keep the chest rotation of the previous key
            var e = new Vector3[17];
            c.Sample(0.52f, e);
            Assert.AreEqual(12f, e[Rigs.Ch].x, 1e-3f);
        }
    }

    public class GaitTests
    {
        [Test]
        public void IdleHasNoStrideAndRunSwingsLegsOpposite()
        {
            var idle = new Vector3[17];
            HumanoidGait.Evaluate(1.2f, 0f, 1f, 0f, 0f, 0f, idle);
            Assert.Less(Mathf.Abs(idle[Rigs.ThR].x - idle[Rigs.ThL].x), 0.01f);

            var run = new Vector3[17];
            HumanoidGait.Evaluate(Mathf.PI * 0.5f, 6f, 1f, 0f, 0f, 0f, run);
            Assert.Less(run[Rigs.ThR].x, -20f, "right thigh swings forward at phase pi/2");
            Assert.Greater(run[Rigs.ThL].x, 20f, "left thigh swings back");
            Assert.Greater(run[Rigs.ArR].x, 10f, "right arm back while right leg forward");
        }

        [Test]
        public void BackwardsMovementReversesTheSwing()
        {
            var fwd = new Vector3[17];
            var back = new Vector3[17];
            HumanoidGait.Evaluate(1f, 4f, 1f, 0f, 0f, 0f, fwd);
            HumanoidGait.Evaluate(1f, 4f, -1f, 0f, 0f, 0f, back);
            Assert.AreEqual(Mathf.Sign(fwd[Rigs.ThR].x), -Mathf.Sign(back[Rigs.ThR].x));
        }

        [Test]
        public void StrideGrowsWithSpeed()
        {
            Assert.Less(HumanoidGait.StrideLength(2f), HumanoidGait.StrideLength(7f));
        }
    }
}
