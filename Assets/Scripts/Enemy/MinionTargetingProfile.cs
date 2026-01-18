using UnityEngine;

public class MinionTargetingProfile : MonoBehaviour
{
    [SerializeField] private MinionStats.Role[] preferredRoles = new MinionStats.Role[0];
    [SerializeField] private bool preferLowestHp = false;

    public MinionStats.Role[] PreferredRoles => preferredRoles;
    public bool PreferLowestHp => preferLowestHp;
}
