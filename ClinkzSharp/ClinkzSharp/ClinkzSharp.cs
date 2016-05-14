namespace ClinkzSharp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Extensions;
    using Ensage.Common.Menu;
    using SharpDX;
    using SharpDX.Direct3D9;

    internal class ClinkzSharp
    {

        private static Ability strafe, arrows, dpAbility;
        private static Item bkb, orchid, hex, medallion, solar, bloodthorn;
        private static readonly Menu Menu = new Menu("ClinkzSharp", "clinkzsharp", true, "npc_dota_hero_Clinkz", true);
        private static Hero me, target;
        private static bool autoKillz;
        private static bool autoFarmz;
        public static float TargetDistance { get; private set; }
        private static AbilityToggler menuValue;
        private static bool dragonLance;
        private static bool menuvalueSet;
        private static readonly int[] Quack = { 0, 50, 60, 70, 80 };
        private static int attackRange;
        private static ParticleEffect effect;
        private static readonly Dictionary<int, ParticleEffect> Effect = new Dictionary<int, ParticleEffect>();
        private static int attackRangeDraw;
        private static Font text;
        private static Font notice;
        private static Line line;


        public static void Init()
        {
            Game.OnUpdate += Game_OnUpdate;
            Game.OnUpdate += Farming;
            Game.OnWndProc += Game_OnWndProc;
            Drawing.OnDraw += Drawing_OnDraw;
            Drawing.OnPreReset += Drawing_OnPreReset;
            Drawing.OnPostReset += Drawing_OnPostReset;
            Drawing.OnEndScene += Drawing_OnEndScene;
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;

            Console.WriteLine(@"> ClinkzSharp LOADED!");
            var menuThingy = new Menu("Options", "opsi");
            menuThingy.AddItem(new MenuItem("enable", "Enable").SetValue(true));
            Menu.AddSubMenu(menuThingy);
            menuThingy.AddItem(new MenuItem("comboKey", "Combo Key").SetValue(new KeyBind(32, KeyBindType.Press)));
            menuThingy.AddItem(new MenuItem("farmKey", "Farm Key").SetValue(new KeyBind('E', KeyBindType.Press)));
            menuThingy.AddItem(new MenuItem("enableult", "Enable AutoUlt").SetValue(true));
            menuThingy.AddItem(new MenuItem("drawLastHit", "Draw Last hit").SetValue(true));
            menuThingy.AddItem(new MenuItem("drawAttackRange", "Draw Clinkz attack range").SetValue(true));
            Menu.AddToMainMenu();

            var dict = new Dictionary<string, bool>
            {
                { "item_solar_crest", true },
                { "item_black_king_bar", true },
                { "item_medallion_of_courage", true },
                { "item_orchid", true },
                { "item_bloodthorn", true },
                { "item_sheepstick", true },

            };
            Menu.AddItem(new MenuItem("Items", "Items:").SetValue(new AbilityToggler(dict)));

            text = new Font(
                Drawing.Direct3DDevice9,
                new FontDescription
                {
                FaceName = "Segoe UI",// Microsoft Sans Serif looks good too
                Height = 17,
                OutputPrecision = FontPrecision.Default,
                 Quality = FontQuality.ClearType
                });

            notice = new Font(
                Drawing.Direct3DDevice9,
                new FontDescription
                {
                    FaceName = "Segoe UI", // Microsoft Sans Serif looks good too
                    Height = 14,
                    OutputPrecision = FontPrecision.Default,
                    Quality = FontQuality.ClearType
                });

            line = new Line(Drawing.Direct3DDevice9);

            OnLoadMessage();
        }

        public static void Game_OnUpdate(EventArgs args)
        {
            if (!Game.IsInGame || Game.IsPaused || Game.IsWatchingGame) return;

            me = ObjectManager.LocalHero;
            if (me == null || me.ClassID != ClassID.CDOTA_Unit_Hero_Clinkz)
                return;

            if (arrows == null)
                arrows = me.Spellbook.SpellW;
           
            if (dpAbility == null)
                dpAbility = me.Spellbook.SpellR;

            if (bkb == null)
                bkb = me.FindItem("item_black_king_bar");

            if (strafe == null)
                strafe = me.Spellbook.Spell1;

            if (hex == null)
                hex = me.FindItem("item_sheepstick");

            if (orchid == null)
                orchid = me.FindItem("item_orchid");

            if (bloodthorn == null)
                bloodthorn = me.FindItem("item_bloodthorn");

            if (medallion == null)
                medallion = me.FindItem("item_medallion_of_courage");

            if (solar == null)
                solar = me.FindItem("item_solar_crest");

            dragonLance = me.Modifiers.Any(x => x.Name == "modifier_item_dragon_lance");

            attackRange = dragonLance ? 760 : 630;

            if (!menuvalueSet)
            {
                menuValue = Menu.Item("Items").GetValue<AbilityToggler>();
                menuvalueSet = true;
            }

            const int DPrange = 0x190;


            var creepR =
                ObjectManager.GetEntities<Unit>()
                    .Where(
                        creep =>
                            (creep.ClassID == ClassID.CDOTA_BaseNPC_Creep_Lane ||
                             creep.ClassID == ClassID.CDOTA_BaseNPC_Creep_Neutral) &&
                             creep.IsAlive && creep.IsVisible && creep.IsSpawned &&
                             creep.Team != me.Team && creep.Position.Distance2D(me.Position) <= DPrange &&
                             me.Spellbook.SpellR.CanBeCasted()).ToList();
            if (autoKillz && Menu.Item("enable").GetValue<bool>())
            {
                target = me.ClosestToMouseTarget(1001);

                //orbwalk
                if (target != null && (!target.IsValid || !target.IsVisible || !target.IsAlive || target.Health <= 0))
                {
                    target = null;
                }
                var canCancel = Orbwalking.CanCancelAnimation();
                if (canCancel)
                {
                    if (target != null && !target.IsVisible && !Orbwalking.AttackOnCooldown(target))
                    {
                        target = me.ClosestToMouseTarget();
                    }
                    else if (target == null || !Orbwalking.AttackOnCooldown(target) && target.HasModifiers(new[]
                                    {
                                        "modifier_dazzle_shallow_grave", "modifier_item_blade_mail_reflect",                                        
                                    }, false))                       
                    {
                        var bestAa = me.BestAATarget();
                        if (bestAa != null)
                        {
                            target = me.BestAATarget();
                        }
                    }
                }
                
                if (target != null && target.IsAlive && !target.IsInvul() && !target.IsIllusion)
                {
                    if (me.CanAttack() && me.CanCast() && !me.IsChanneling())
                    {
                        TargetDistance = me.Position.Distance2D(target);

                        Orbwalking.Orbwalk(target, Game.Ping, attackmodifiers: true);
                       
                        if (creepR.Count > 0 && !me.Modifiers.ToList().Exists(x => x.Name == "modifier_clinkz_death_pact") && Menu.Item("enableult").GetValue<bool>())
                        {
                            var creepmax = creepR.MaxOrDefault(x => x.Health);
                            dpAbility.UseAbility(creepmax);
                        }

                        if (medallion != null && medallion.IsValid && medallion.CanBeCasted() && Utils.SleepCheck("medallion") && menuValue.IsEnabled(medallion.Name) && me.Distance2D(target) <= attackRange + 90)
                        {
                            medallion.UseAbility(target);
                            Utils.Sleep(50 + Game.Ping, "medallion");
                        }

                        if (solar != null && solar.IsValid && solar.CanBeCasted() && Utils.SleepCheck("solar") && menuValue.IsEnabled(solar.Name))
                        {
                            solar.UseAbility(target);
                            Utils.Sleep(50 + Game.Ping, "solar");
                        }

                        if (bkb != null && bkb.IsValid && bkb.CanBeCasted() && Utils.SleepCheck("bkb") && menuValue.IsEnabled(bkb.Name) && me.Distance2D(target) <= attackRange + 90)
                        {
                            bkb.UseAbility();
                            Utils.Sleep(150 + Game.Ping, "bkb");
                        }

                        if (hex != null && hex.IsValid && hex.CanBeCasted() && Utils.SleepCheck("hex") && menuValue.IsEnabled(hex.Name))
                        {
                            hex.CastStun(target);
                            Utils.Sleep(250 + Game.Ping, "hex");
                            return;
                        }

                        if (orchid != null && orchid.IsValid && orchid.CanBeCasted() && Utils.SleepCheck("orchid") && menuValue.IsEnabled(orchid.Name))
                        {
                            orchid.CastStun(target);
                            Utils.Sleep(250 + Game.Ping, "orchid");
                            return;
                        }

                        if (bloodthorn != null && bloodthorn.IsValid && bloodthorn.CanBeCasted() && Utils.SleepCheck("bloodthorn") && menuValue.IsEnabled(bloodthorn.Name))
                        {
                            bloodthorn.CastStun(target);
                            Utils.Sleep(250 + Game.Ping, "orchid");
                            return;
                        }

                        if (!me.IsAttacking() && me.Distance2D(target) >= attackRange && Utils.SleepCheck("follow"))
                        {
                            me.Move(Game.MousePosition);
                            Utils.Sleep(150 + Game.Ping, "follow");
                        }
                    }              
                }
                else
                {
                    me.Move(Game.MousePosition);
                }
            }
        }//gameOnUpdate Close.

        private static void OnLoadMessage()
        {
            Game.PrintMessage("<font face='helvetica' color='#00FF00'>ClinkzSharp loaded!</font>", MessageType.LogMessage);
            Console.WriteLine(@"> ClinkzSharp is on like Donkey Kong!");
        }

        public static void Farming(EventArgs args)
        {
            if (!Game.IsInGame || Game.IsPaused || Game.IsWatchingGame) return;

            me = ObjectManager.LocalHero;
            if (me == null || me.ClassID != ClassID.CDOTA_Unit_Hero_Clinkz)
                return;

            var quacklvl = me.Spellbook.SpellW.Level;

            if (autoFarmz)
            {
                var creepW =
                ObjectManager.GetEntities<Unit>()
                    .Where(
                        creep =>
                            (creep.ClassID == ClassID.CDOTA_BaseNPC_Creep_Lane ||
                             creep.ClassID == ClassID.CDOTA_BaseNPC_Creep_Siege ||
                             creep.ClassID == ClassID.CDOTA_Unit_VisageFamiliar ||
                             creep.ClassID == ClassID.CDOTA_BaseNPC_Creep_Neutral ||
                             creep.ClassID == ClassID.CDOTA_Unit_SpiritBear ||
                             creep.ClassID == ClassID.CDOTA_BaseNPC_Invoker_Forged_Spirit ||
                             creep.ClassID == ClassID.CDOTA_BaseNPC_Creep) &&
                             creep.IsAlive && creep.IsVisible && creep.IsSpawned &&
                             creep.Team != me.Team && creep.Health <= Math.Floor((Quack[quacklvl] + me.DamageAverage) * (1 - creep.MagicDamageResist)) 
                             && creep.Position.Distance2D(me.Position) <= attackRange ).ToList();
                {
                    if (creepW.Count > 0)
                    {
                        var creepmax = creepW.MaxOrDefault(x => x.Health);
                        me.Spellbook.SpellW.UseAbility(creepmax);
                    }
                    else
                    {
                        me.Move(Game.MousePosition);
                    }
                }
            }
        }//Farming Close.
        private static void Game_OnWndProc(WndEventArgs args)
        {
            if (!Game.IsChatOpen)
            {

                if (Menu.Item("comboKey").GetValue<KeyBind>().Active)
                {
                    autoKillz = true;
                }
                else
                {
                    autoKillz = false;
                }

                if (Menu.Item("farmKey").GetValue<KeyBind>().Active)
                {
                    autoFarmz = true;
                }
                else
                {
                    autoFarmz = false;
                }
            }
        }//game_onWndProc
        private static double getDmgOnUnit(Unit unit, double bonusdamage)
        {

            var quacklvl = me.Spellbook.SpellW.Level;
            var quelling_blade = me.FindItem("item_quelling_blade");
            double physDamage = me.MinimumDamage + me.BonusDamage;
            if (quelling_blade != null)
            {
                if (me.ClassID == ClassID.CDOTA_Unit_Hero_Clinkz)
                {
                    physDamage = me.MinimumDamage * 1.25 + me.BonusDamage;
                }
            }
            var damageMp = 1 - 0.06 * unit.Armor / (1 + 0.06 * Math.Abs(unit.Armor));

            var realDamage = (bonusdamage + physDamage + Quack[quacklvl]) * damageMp;
            if (unit.ClassID == ClassID.CDOTA_BaseNPC_Creep_Siege ||
                unit.ClassID == ClassID.CDOTA_BaseNPC_Tower)
            {
                realDamage = realDamage / 2;
            }

            return realDamage;
        }
        private static void drawHPLastHit()
        {
            var enemies = ObjectManager.GetEntities<Unit>().Where(x => (x.ClassID == ClassID.CDOTA_BaseNPC_Tower || x.ClassID == ClassID.CDOTA_BaseNPC_Creep_Lane || x.ClassID == ClassID.CDOTA_BaseNPC_Creep || x.ClassID == ClassID.CDOTA_BaseNPC_Creep_Neutral || x.ClassID == ClassID.CDOTA_BaseNPC_Creep_Siege || x.ClassID == ClassID.CDOTA_BaseNPC_Additive || x.ClassID == ClassID.CDOTA_BaseNPC_Building || x.ClassID == ClassID.CDOTA_BaseNPC_Creature) && x.IsAlive && x.IsVisible && x.Team != me.Team && x.Distance2D(me) < attackRange + 600);

            foreach (var enemy in enemies.Where(x => true))
            {
                var health = enemy.Health;
                var maxHealth = enemy.MaximumHealth;
                if (health == maxHealth)
                {
                    continue;
                }
                var damge = (float)getDmgOnUnit(enemy, 0);
                var hpleft = health;
                var hpperc = hpleft / maxHealth;

                var hbarpos = HUDInfo.GetHPbarPosition(enemy);

                Vector2 screenPos;
                var enemyPos = enemy.Position + new Vector3(0, 0, enemy.HealthBarOffset);
                if (!Drawing.WorldToScreen(enemyPos, out screenPos))
                {
                    continue;
                }

                var start = screenPos;

                hbarpos.X = start.X - HUDInfo.GetHPBarSizeX(enemy) / 2;
                hbarpos.Y = start.Y;
                var hpvarx = hbarpos.X;
                var a = (float)Math.Round(damge * HUDInfo.GetHPBarSizeX(enemy) / enemy.MaximumHealth);
                var position = hbarpos + new Vector2(hpvarx * hpperc + 10, -12);
                try
                {
                    Drawing.DrawRect(position, new Vector2(a, HUDInfo.GetHpBarSizeY(enemy) - 4), enemy.Health > damge ? enemy.Health > damge * 2 ? new Color(180, 205, 205, 40) : new Color(255, 0, 0, 60) : new Color(127, 255, 0, 80));
                    Drawing.DrawRect(position, new Vector2(a, HUDInfo.GetHpBarSizeY(enemy) - 4), Color.Black, true);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        private static void Drawing_OnPostReset(EventArgs args)
        {
            text.OnResetDevice();
            notice.OnResetDevice();
            line.OnResetDevice();
        }

        private static void Drawing_OnPreReset(EventArgs args)
        {
            text.OnLostDevice();
            notice.OnLostDevice();
            line.OnLostDevice();
        }

        public static void DrawFilledBox(float x, float y, float w, float h, Color color)
        {
            var vLine = new Vector2[2];

            line.GLLines = true;
            line.Antialias = false;
            line.Width = w;

            vLine[0].X = x + w / 2;
            vLine[0].Y = y;
            vLine[1].X = x + w / 2;
            vLine[1].Y = y + h;

            line.Begin();
            line.Draw(vLine, color);
            line.End();
        }

        public static void DrawBox(float x, float y, float w, float h, float px, Color color)
        {
            DrawFilledBox(x, y + h, w, px, color);
            DrawFilledBox(x - px, y, px, h, color);
            DrawFilledBox(x, y - px, w, px, color);
            DrawFilledBox(x + w, y, px, h, color);
        }

        public static void DrawShadowText(string stext, int x, int y, Color color, Font f)
        {
            f.DrawText(null, stext, x + 1, y + 1, Color.Black);
            f.DrawText(null, stext, x, y, color);
        }

        private static void Drawing_OnEndScene(EventArgs args)
        {
            if (Drawing.Direct3DDevice9 == null || Drawing.Direct3DDevice9.IsDisposed || !Game.IsInGame)
            {
                return;
            }

            var player = ObjectManager.LocalPlayer;
            me = ObjectManager.LocalHero;
            if (player == null || player.Team == Team.Observer || me.ClassID != ClassID.CDOTA_Unit_Hero_Clinkz)
            {
                return;
            }

            if (Menu.Item("comboKey").GetValue<KeyBind>().Active)
            {
                DrawBox(2, 45, 115, 20, 1, new ColorBGRA(0, 128, 0, 128));
                DrawFilledBox(2, 45, 115, 20, new ColorBGRA(0, 0, 0, 100));
                DrawShadowText("Clinkz#: Comboing!", 2, 45, Color.LightBlue, text);
            }
            if (Menu.Item("enableult").GetValue<bool>())
            {
                DrawBox(120, 45, 65, 20, 1, new ColorBGRA(0, 200, 100, 100));
                DrawFilledBox(120, 45, 65, 20, new ColorBGRA(0, 0, 0, 100));
                DrawShadowText("AutoUlt On", 120, 45, Color.LightBlue, text);
            }
            else
            {
                DrawBox(120, 45, 65, 20, 1, new ColorBGRA(0, 200, 100, 100));
                DrawFilledBox(120, 45, 65, 20, new ColorBGRA(0, 0, 0, 100));
                DrawShadowText("AutoUlt Off", 120, 45, Color.LightBlue, text);
            }
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            if (!Game.IsInGame || Game.IsPaused || Game.IsWatchingGame)
            {
                return;
            }

            me = ObjectManager.LocalHero;
            if (me == null || me.ClassID != ClassID.CDOTA_Unit_Hero_Clinkz)
            {
                return;
            }

            //newer stuff begunnn
            if (attackRange != attackRangeDraw)
            {
                attackRangeDraw = attackRange;
                if (Effect.TryGetValue(3, out effect))
                {
                    effect.Dispose();
                    Effect.Remove(3);
                }
                if (!Effect.TryGetValue(3, out effect))
                {
                    effect = me.AddParticleEffect(@"particles\ui_mouseactions\drag_selected_ring.vpcf");
                    effect.SetControlPoint(1, new Vector3(255, 255, 255));
                    effect.SetControlPoint(2, new Vector3(attackRange + 70, 255, 0));
                    Effect.Add(3, effect);
                }
            }
            if (Menu.Item("drawAttackRange").GetValue<bool>())
            {
                if (!Effect.TryGetValue(3, out effect))
                {
                    effect = me.AddParticleEffect(@"particles\ui_mouseactions\drag_selected_ring.vpcf");
                    effect.SetControlPoint(1, new Vector3(255, 255, 255));
                    effect.SetControlPoint(2, new Vector3(attackRange + 70, 255, 0));
                    Effect.Add(3, effect);
                }
            }
            else
            {
                if (Effect.TryGetValue(3, out effect))
                {
                    effect.Dispose();
                    Effect.Remove(3);
                }
            }

            //new stuff ends 
            if (Menu.Item("comboKey").GetValue<KeyBind>().Active)
            {
                if (target == null || !target.IsAlive)
                {
                    return;
                }
                var pos = Drawing.WorldToScreen(target.Position);
                Drawing.DrawText("Target", pos, new Vector2(0, 50), Color.Red, FontFlags.AntiAlias | FontFlags.DropShadow);
            }

            if (Menu.Item("drawLastHit").GetValue<bool>())
            {
                drawHPLastHit();
            }
        } //DRAWS

        private static void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            text.Dispose();
            notice.Dispose();
            line.Dispose();
        }
    }
} // close end or we
