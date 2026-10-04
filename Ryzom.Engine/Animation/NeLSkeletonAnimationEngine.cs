using System;
using System.Collections.Generic;
using System.Numerics;

namespace Ryzom.Engine.Animation;

/// <summary>
/// Bone node in the NeL 3D skeletal hierarchy.
/// </summary>
public record SkeletalBone(
    int BoneId,
    string BoneName,
    int ParentBoneId,
    Vector3 LocalTranslation,
    Quaternion LocalRotation,
    Matrix4x4 InverseBindMatrix
);

/// <summary>
/// Keyframe for a specific bone in an animation track.
/// </summary>
public record BoneKeyframe(
    float TimestampSeconds,
    Vector3 Translation,
    Quaternion Rotation,
    Vector3 Scale
);

/// <summary>
/// Named animation clip containing keyframe tracks per bone.
/// </summary>
public record AnimationClip(
    string ClipName,
    float DurationSeconds,
    bool IsLooping,
    Dictionary<int, List<BoneKeyframe>> BoneTracks
);

/// <summary>
/// 4-bone vertex skinning influence weight.
/// </summary>
public record VertexSkinningWeight(
    (int B0, int B1, int B2, int B3) BoneIndices,
    (float W0, float W1, float W2, float W3) Weights
);

/// <summary>
/// Socket attachment anchor on a skeleton (e.g. RightHandWeapon, ShieldMount, HelmetSocket).
/// </summary>
public record SkeletonSocket(
    string SocketName,
    int AttachedBoneId,
    Vector3 RelativeOffset,
    Quaternion RelativeRotation
);

/// <summary>
/// Engine for NeL skeletal rigging, keyframe evaluation, quaternion slerp blending,
/// and 4-bone influence vertex skinning.
/// </summary>
public class NeLSkeletonAnimationEngine
{
    private readonly List<SkeletalBone> _bones = new();
    private readonly Dictionary<string, AnimationClip> _clips = new();
    private readonly Dictionary<string, SkeletonSocket> _sockets = new();

    public int BoneCount => _bones.Count;
    public int ClipCount => _clips.Count;
    public int SocketCount => _sockets.Count;

    public void AddBone(SkeletalBone bone)
    {
        _bones.Add(bone);
    }

    public void RegisterClip(AnimationClip clip)
    {
        _clips[clip.ClipName] = clip;
    }

    public void RegisterSocket(SkeletonSocket socket)
    {
        _sockets[socket.SocketName] = socket;
    }

    public SkeletalBone? GetBone(int boneId)
    {
        return boneId >= 0 && boneId < _bones.Count ? _bones[boneId] : null;
    }

    public SkeletonSocket? GetSocket(string socketName)
    {
        return _sockets.TryGetValue(socketName, out var s) ? s : null;
    }

    /// <summary>
    /// Evaluates interpolated local transform for a bone at a specific timestamp.
    /// </summary>
    public (Vector3 Translation, Quaternion Rotation, Vector3 Scale) EvaluateBoneTrack(
        AnimationClip clip,
        int boneId,
        float timeSeconds)
    {
        if (!clip.BoneTracks.TryGetValue(boneId, out var track) || track.Count == 0)
        {
            var fallback = GetBone(boneId);
            return fallback != null
                ? (fallback.LocalTranslation, fallback.LocalRotation, Vector3.One)
                : (Vector3.Zero, Quaternion.Identity, Vector3.One);
        }

        float normalizedTime = clip.IsLooping
            ? (timeSeconds % clip.DurationSeconds)
            : Math.Clamp(timeSeconds, 0f, clip.DurationSeconds);

        if (track.Count == 1 || normalizedTime <= track[0].TimestampSeconds)
        {
            return (track[0].Translation, track[0].Rotation, track[0].Scale);
        }

        if (normalizedTime >= track[^1].TimestampSeconds)
        {
            return (track[^1].Translation, track[^1].Rotation, track[^1].Scale);
        }

        for (int i = 0; i < track.Count - 1; i++)
        {
            var k0 = track[i];
            var k1 = track[i + 1];

            if (normalizedTime >= k0.TimestampSeconds && normalizedTime <= k1.TimestampSeconds)
            {
                float factor = (normalizedTime - k0.TimestampSeconds) / (k1.TimestampSeconds - k0.TimestampSeconds);
                var trans = Vector3.Lerp(k0.Translation, k1.Translation, factor);
                var rot = Quaternion.Slerp(k0.Rotation, k1.Rotation, factor);
                var scale = Vector3.Lerp(k0.Scale, k1.Scale, factor);
                return (trans, rot, scale);
            }
        }

        return (track[^1].Translation, track[^1].Rotation, track[^1].Scale);
    }

    /// <summary>
    /// Computes world matrices for all bones in the skeleton at a given animation time.
    /// </summary>
    public Matrix4x4[] EvaluateSkeletonPose(AnimationClip clip, float timeSeconds)
    {
        var worldMatrices = new Matrix4x4[_bones.Count];

        for (int i = 0; i < _bones.Count; i++)
        {
            var bone = _bones[i];
            var (trans, rot, scale) = EvaluateBoneTrack(clip, bone.BoneId, timeSeconds);

            var localMat = Matrix4x4.CreateScale(scale) *
                           Matrix4x4.CreateFromQuaternion(rot) *
                           Matrix4x4.CreateTranslation(trans);

            if (bone.ParentBoneId >= 0 && bone.ParentBoneId < i)
            {
                worldMatrices[i] = localMat * worldMatrices[bone.ParentBoneId];
            }
            else
            {
                worldMatrices[i] = localMat;
            }
        }

        return worldMatrices;
    }

    /// <summary>
    /// Blends two animation poses with cross-fade weight (e.g. Walk to Run or Walk to Harvest Swing).
    /// </summary>
    public Matrix4x4[] BlendPoses(Matrix4x4[] poseA, Matrix4x4[] poseB, float blendWeight)
    {
        blendWeight = Math.Clamp(blendWeight, 0f, 1f);
        int count = Math.Min(poseA.Length, poseB.Length);
        var blended = new Matrix4x4[count];

        for (int i = 0; i < count; i++)
        {
            // Linear matrix interpolation for skinning matrices
            blended[i] = Matrix4x4.Lerp(poseA[i], poseB[i], blendWeight);
        }

        return blended;
    }

    /// <summary>
    /// Deforms a vertex position using 4-bone skinning influences: p' = sum(w_i * M_i * p).
    /// </summary>
    public Vector3 SkinVertex(Vector3 restPosition, Matrix4x4[] currentPose, VertexSkinningWeight weight)
    {
        Vector3 skinned = Vector3.Zero;

        if (weight.Weights.W0 > 0 && weight.BoneIndices.B0 < currentPose.Length)
        {
            var m = _bones[weight.BoneIndices.B0].InverseBindMatrix * currentPose[weight.BoneIndices.B0];
            skinned += Vector3.Transform(restPosition, m) * weight.Weights.W0;
        }

        if (weight.Weights.W1 > 0 && weight.BoneIndices.B1 < currentPose.Length)
        {
            var m = _bones[weight.BoneIndices.B1].InverseBindMatrix * currentPose[weight.BoneIndices.B1];
            skinned += Vector3.Transform(restPosition, m) * weight.Weights.W1;
        }

        if (weight.Weights.W2 > 0 && weight.BoneIndices.B2 < currentPose.Length)
        {
            var m = _bones[weight.BoneIndices.B2].InverseBindMatrix * currentPose[weight.BoneIndices.B2];
            skinned += Vector3.Transform(restPosition, m) * weight.Weights.W2;
        }

        if (weight.Weights.W3 > 0 && weight.BoneIndices.B3 < currentPose.Length)
        {
            var m = _bones[weight.BoneIndices.B3].InverseBindMatrix * currentPose[weight.BoneIndices.B3];
            skinned += Vector3.Transform(restPosition, m) * weight.Weights.W3;
        }

        return skinned;
    }

    /// <summary>
    /// Evaluates the world position and orientation of an attachment socket (e.g. weapon in right hand).
    /// </summary>
    public (Vector3 Position, Quaternion Rotation) EvaluateSocketTransform(string socketName, Matrix4x4[] currentPose)
    {
        if (!_sockets.TryGetValue(socketName, out var socket) || socket.AttachedBoneId >= currentPose.Length)
        {
            return (Vector3.Zero, Quaternion.Identity);
        }

        var boneMatrix = currentPose[socket.AttachedBoneId];
        var socketLocalMat = Matrix4x4.CreateFromQuaternion(socket.RelativeRotation) *
                             Matrix4x4.CreateTranslation(socket.RelativeOffset);
        var worldMat = socketLocalMat * boneMatrix;

        Matrix4x4.Decompose(worldMat, out _, out var rot, out var trans);
        return (trans, rot);
    }
}
