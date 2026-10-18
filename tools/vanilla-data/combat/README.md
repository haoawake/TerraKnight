# 武器原版基准

`vanilla-runtime.json` 由 `tools/RecipeAudit/DumpCrafting.cs` 调用本机 Terraria 1.4.5.8 的 `Item.SetDefaults` 生成。`source.json` 记录原版程序版本和 SHA-256。该程序只读取原版程序集，不启动世界、不修改游戏文件或玩家存档。

本次扩充了 `useTime`、`useAnimation`、`reuseDelay`、`channel`、`noMelee`、`crit`、`shootSpeed`、`mana` 字段。6196 种物品的既有字段、3603 条原版配方及配方组与先前制作审计数据逐项相同。

运行 `python tools/generate_weapon_baseline.py` 可重新生成 `src/TerraKnight/Items/VanillaWeaponDamage.cs`。生成器拒绝缺少时序字段或未完成配方初始化的数据，不从已经平衡过的 mod 物品反推原版伤害。

伤害、攻击时序以此数据为原版参考；弹幕数、伤害倍率、独立冷却和持续命中从实际运行的发射与命中实现核对，分别见 `WeaponBalance.cs` 和 `WeaponProjectileBudget.cs`。游戏内 `weaponbalance` 测试导出静态估算与实际敌人失血记录，二者单独报告。

## 护甲与套装原版基准

`vanilla-armor-bonuses.json` 由 `tools/RecipeAudit/DumpArmor.cs` 直接调用同一 Terraria 程序的 `Player.GrantArmorBenefits` 和 `ArmorSetBonuses.Benefits` 回调生成，包含 653 个头部/身体/腿部物品引用和 64 种原版套装回调。生成器将金属、木材等共享回调展开为 mod 的 75 个套装标识。`python tools/generate_armor_baseline.py` 可重新生成测试引用。

采样使用独立 Player 实例、记录回调前后差值；鞭子使用时间乘数预置为 1，避免构造函数默认 0 导致乘法加成丢失。星尘和禁戒回调依赖世界中的实体，独立采样记录其错误并仅用于核对静态数值；对应守卫实体和沙暴能力另在隔离游戏中验证。动态护盾、闪避、隐身、反击等通过实际 mod 游戏实现验证，不以静态数据声称它们已生效。
