using UnityEngine;

[DisallowMultipleComponent]
public class AttachPropToRightHand : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private Transform prop;

    [Header("Auto Find")]
    [SerializeField] private string characterObjectName;
    [SerializeField] private string propObjectName;

    [Header("Hand Offset")]
    [SerializeField] private Vector3 localPositionOffset;
    [SerializeField] private Vector3 localEulerRotationOffset;

    [Header("Runtime")]
    [SerializeField] private bool attachOnStart = true;

    private void Reset()
    {
        characterAnimator = GetComponent<Animator>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        if (attachOnStart)
        {
            Attach();
        }
    }

    public void Attach()
    {
        ResolveReferences();

        if (characterAnimator == null)
        {
            Debug.LogError($"{nameof(AttachPropToRightHand)} needs an Animator reference.", this);
            return;
        }

        if (prop == null)
        {
            Debug.LogError($"{nameof(AttachPropToRightHand)} needs a prop Transform reference.", this);
            return;
        }

        Avatar avatar = characterAnimator.avatar;
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            Debug.LogError($"{nameof(AttachPropToRightHand)} requires a valid Humanoid avatar.", this);
            return;
        }

        Transform rightHand = characterAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        if (rightHand == null)
        {
            Debug.LogError("Could not find the character's right hand bone.", this);
            return;
        }

        prop.SetParent(rightHand, false);
        prop.localPosition = Vector3.zero;
        prop.localRotation = Quaternion.identity;

        prop.localPosition = localPositionOffset;
        prop.localRotation = Quaternion.Euler(localEulerRotationOffset);
    }

    private void ResolveReferences()
    {
        if (characterAnimator == null)
        {
            characterAnimator = GetComponent<Animator>();
        }

        if (characterAnimator == null && !string.IsNullOrEmpty(characterObjectName))
        {
            GameObject character = GameObject.Find(characterObjectName);
            if (character != null)
            {
                characterAnimator = character.GetComponentInChildren<Animator>();
            }
        }

        if (prop == null && !string.IsNullOrEmpty(propObjectName))
        {
            GameObject propObject = GameObject.Find(propObjectName);
            if (propObject != null)
            {
                prop = propObject.transform;
            }
        }
    }
}
