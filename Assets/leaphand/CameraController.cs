using Leap;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

/// <summary> 
/// カメラ制御
/// </summary>
public class CameraController : MonoBehaviour
{
    /** カメラの移動スピード */
    private float speed = 0.025f;

    /** LeapMotionのコントローラー */
    private Controller controller;

    /** エントリポイント */
    void Start()
    {
        // LeapMotionのコントローラー
        controller = new Controller();

        // LeapMotionから手の情報を取得
        var handsStream = this.UpdateAsObservable()
            .Select(_ => controller.Frame().Hands);

        // 両手グー開始判定ストリーム
        var beginDoubleRockGripStream = handsStream
            .Where(hands => IsDoubleRockGrip(hands));

        // 両手グー終了判定ストリーム
        var endDoubleRockGripStream = handsStream
            .Where(hands => !IsDoubleRockGrip(hands));

        // カメラ拡縮
        beginDoubleRockGripStream
            .Select(hands => hands[0].PalmPosition.DistanceTo(hands[1].PalmPosition))
            .Where(distance => distance > 0.0f)
            .Buffer(2, 1)
            .Select(distances => distances[1] / distances[0])
            .TakeUntil(endDoubleRockGripStream).RepeatUntilDestroy(this)
            .Where(distanceRate => distanceRate > 0.0f)
            .Subscribe(distanceRate => transform.localScale /= distanceRate);

        // カメラ回転
        beginDoubleRockGripStream
            .Select(hands => ToVector3(hands[1].PalmPosition - hands[0].PalmPosition))
            .Where(diff => diff.magnitude > 0.0f)
            .Buffer(2, 1)
            .Select(diffs => Quaternion.AngleAxis(Vector3.Angle(diffs[0], diffs[1]), Vector3.Cross(diffs[1], diffs[0])))
            .TakeUntil(endDoubleRockGripStream).RepeatUntilDestroy(this)
            .Subscribe(quaternion => transform.rotation *= quaternion);

        // カメラ移動
        beginDoubleRockGripStream
            .Select(hands => ToVector3((hands[0].PalmPosition + hands[1].PalmPosition) * 0.5f))
            .Buffer(2, 1)
            .Select(positions => positions[1] - positions[0])
            .TakeUntil(endDoubleRockGripStream).RepeatUntilDestroy(this)
            .Subscribe(movement => transform.Translate(-speed * movement));
    }

    /** 両手グーかどうか */
    public bool IsDoubleRockGrip(List<Hand> hands)
    {
        return
            hands.Count == 2 &&
            hands[0].Fingers.ToArray().Count(x => x.IsExtended) == 0 &&
            hands[1].Fingers.ToArray().Count(x => x.IsExtended) == 0;
    }

    /** LeapのVectorからUnityのVector3に変換 */
    Vector3 ToVector3(Vector v)
    {
        return new Vector3(v.x, v.y, -v.z);
    }
}