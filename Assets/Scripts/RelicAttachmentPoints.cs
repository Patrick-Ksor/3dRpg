using UnityEngine;

/// <summary>
/// Add this component to a character prefab and wire up the socket Transforms in the Inspector.
/// Each socket should be an empty child GameObject positioned at the desired attachment point
/// on the character's skeleton (e.g. parented directly to the hand or head bone).
/// </summary>
public class RelicAttachmentPoints : MonoBehaviour
{
    [Tooltip("Empty child GO placed at the weapon grip (right hand bone).")]
    public Transform rightHand;

    [Tooltip("Empty child GO placed at the off-hand or shield grip (left hand bone).")]
    public Transform leftHand;

    [Tooltip("Empty child GO placed at the crown of the head (head bone).")]
    public Transform head;

    [Tooltip("Empty child GO placed between the shoulder blades (upper spine bone).")]
    public Transform back;

    public Transform GetSocket(RelicSocket socket)
    {
        return socket switch
        {
            RelicSocket.RightHand => rightHand,
            RelicSocket.LeftHand  => leftHand,
            RelicSocket.Head      => head,
            RelicSocket.Back      => back,
            _                     => null,
        };
    }
}
