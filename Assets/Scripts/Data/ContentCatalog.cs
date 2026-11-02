using UnityEngine;

namespace SurvivorArena.Data
{
    /// <summary>
    /// 局内内容目录：本局可出现的全部武器/被动定义。
    /// 一个资产装 10 把武器 + 10 件被动，运行时只读；新增内容只要往数组里加定义。
    /// </summary>
    [CreateAssetMenu(fileName = "ContentCatalog", menuName = "Survivor Arena/Content Catalog")]
    public sealed class ContentCatalog : ScriptableObject
    {
        [SerializeField] private WeaponDefinition[] weapons;
        [SerializeField] private PassiveDefinition[] passives;
        [SerializeField] private Sprite xpGemGreen;
        [SerializeField] private Sprite xpGemRed;

        public WeaponDefinition[] Weapons => weapons;
        public PassiveDefinition[] Passives => passives;
        public Sprite XpGemGreen => xpGemGreen;
        public Sprite XpGemRed => xpGemRed;

        /// <summary>仅供编辑器场景构建器写入；运行时不调用。</summary>
        public void SetContent(
            WeaponDefinition[] weaponDefinitions,
            PassiveDefinition[] passiveDefinitions,
            Sprite gemGreen,
            Sprite gemRed)
        {
            weapons = weaponDefinitions;
            passives = passiveDefinitions;
            xpGemGreen = gemGreen;
            xpGemRed = gemRed;
        }

        public WeaponDefinition FindWeapon(string id)
        {
            if (weapons == null)
            {
                return null;
            }

            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] != null && weapons[i].Id == id)
                {
                    return weapons[i];
                }
            }

            return null;
        }

        public PassiveDefinition FindPassive(string id)
        {
            if (passives == null)
            {
                return null;
            }

            for (int i = 0; i < passives.Length; i++)
            {
                if (passives[i] != null && passives[i].Id == id)
                {
                    return passives[i];
                }
            }

            return null;
        }
    }
}
