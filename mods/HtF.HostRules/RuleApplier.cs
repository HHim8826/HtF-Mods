using System;
using System.Reflection;
using HarmonyLib;

namespace HtF.HostRules
{
    /// <summary>
    /// 套用房主規則。
    ///
    /// 這裡刻意**不去 patch 那些 getter**。
    /// ServerSettings.HealthMultiplier / DamageMultiplier 是極小的 auto-property，
    /// 而 Creature.MaxHp 長這樣：
    ///     return (int)((float)this._maxHp * ServerSettings.HealthMultiplier);
    /// Mono 會把這種一行 getter 直接 inline 進呼叫端，patch 上去會**靜靜地不生效**
    /// ——沒有錯誤、沒有 log，只是沒作用，這種 bug 最難查。
    ///
    /// 所以改成寫入實際的值：遊戲什麼時候設定它們（OnDifficultyChange），
    /// 我們就在後面覆蓋掉。讀取端不管有沒有被 inline 都會拿到我們的值。
    ///
    /// 友傷與一擊必殺同理，走遊戲自己的 ToggleFriendlyFire / ToggleOneShot，
    /// 這樣 SyncVar 會正常同步給其他客戶端，而不是只有房主端看起來不一樣。
    /// </summary>
    internal static class RuleApplier
    {
        private static MethodInfo _setHealth, _setDamage;
        private static bool _resolved;

        // 遊戲在 ServerSettings.OnDifficultyChange 裡的對照表，
        // 用來在關掉覆寫時把值還原回去。
        private static void GameDefaults(Difficulty d, out float health, out float damage)
        {
            switch (d)
            {
                case Difficulty.Easy: health = 0.75f; damage = 0.5f; break;
                case Difficulty.Hard: health = 1.25f; damage = 1.25f; break;
                default: health = 1f; damage = 1f; break;
            }
        }

        internal static bool IsHost
        {
            get
            {
                ServerSettings s = ServerSettings.Instance;
                return s && s.IsServerInitialized;
            }
        }

        internal static void ApplyAll()
        {
            if (!IsHost) return;
            ApplyMultipliers();
            ApplyToggles();
            VitalsTuner.ReapplyAll();
        }

        internal static void ApplyMultipliers()
        {
            if (!IsHost) return;
            Resolve();
            if (_setHealth == null || _setDamage == null) return;

            float health, damage;
            if (Plugin.OverrideDifficulty.Value)
            {
                health = Plugin.CreatureHealthMul.Value;
                damage = Plugin.PlayerDamageTakenMul.Value;
            }
            else
            {
                GameDefaults(ServerSettings.Difficulty, out health, out damage);
            }

            try
            {
                _setHealth.Invoke(null, new object[] { health });
                _setDamage.Invoke(null, new object[] { damage });
            }
            catch (Exception e) { Plugin.Log.LogError("寫入難度乘數失敗：" + e); }
        }

        internal static void ApplyToggles()
        {
            ServerSettings s = ServerSettings.Instance;
            if (!s || !s.IsServerInitialized) return;

            try
            {
                if (Plugin.FriendlyFire.Value != Force.不變)
                {
                    bool want = Plugin.FriendlyFire.Value == Force.強制開啟;
                    if (ServerSettings.UseFriendlyFire != want) s.ToggleFriendlyFire(want);
                }

                if (Plugin.OneShot.Value != Force.不變)
                {
                    bool want = Plugin.OneShot.Value == Force.強制開啟;
                    // 遊戲只給 toggle，沒有 set，所以先讀再決定要不要翻
                    if (ServerSettings.OneShotEnabled != want) s.ToggleOneShot();
                }
            }
            catch (Exception e) { Plugin.Log.LogError("套用規則開關失敗：" + e); }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            _setHealth = AccessTools.PropertySetter(typeof(ServerSettings), "HealthMultiplier");
            _setDamage = AccessTools.PropertySetter(typeof(ServerSettings), "DamageMultiplier");

            if (_setHealth == null || _setDamage == null)
                Plugin.Log.LogWarning("找不到 ServerSettings 的乘數 setter（遊戲可能又更新了），難度覆寫不會生效。");
        }
    }
}
