namespace SlarkSharp
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Extensions;
    using Ensage.Common.Menu;
    using SharpDX;
    using SharpDX.Direct3D9;

    internal class SlarkSharp
    {
        private static readonly Menu Menu = new Menu("SlarkSharp", "slarksharp", true, "npc_dota_hero_Slark", true);
        private static bool autoKillz;
        private static Hero me, target;
        private static Ability darkPact, pounce, shadowDance;
        private static bool trying = false;
        private static Item bkb, orchid, abyssalBlade;
        private static Font text;
        private static Font notice;
        private static Line line;


        public static void Init()
        {
            Game.OnUpdate += Game_OnUpdate;
            Game.OnWndProc += Game_OnWndProc;
            Drawing.OnDraw += Drawing_OnDraw;
            Drawing.OnPreReset += Drawing_OnPreReset;
            Drawing.OnPostReset += Drawing_OnPostReset;
            Drawing.OnEndScene += Drawing_OnEndScene;
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;

            var menuThingy = new Menu("Options", "opsi");
            menuThingy.AddItem(new MenuItem("enable", "Enable").SetValue(true));
            menuThingy.AddItem(new MenuItem("comboKey", "Combo Key").SetValue(new KeyBind(32, KeyBindType.Press)));
            menuThingy.AddItem(new MenuItem("sdtog", "Use Shadow Dance").SetValue(true));
            menuThingy.AddItem(new MenuItem("sdhealth", "If Health % is:").SetValue(new Slider(15)));
            Menu.AddSubMenu(menuThingy);
            Menu.AddToMainMenu();

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

        }

        public static async void Game_OnUpdate(EventArgs args)
        {
            if (!Game.IsInGame || Game.IsPaused || Game.IsWatchingGame) return;

            me = ObjectManager.LocalHero;
            if (me == null || me.ClassID != ClassID.CDOTA_Unit_Hero_Slark)
                return;

            if (darkPact == null)
                darkPact = me.Spellbook.SpellQ;

            if (pounce == null)
                pounce = me.Spellbook.SpellW;

            if (shadowDance == null)
                shadowDance = me.Spellbook.SpellR;

            if (bkb == null)
                bkb = me.FindItem("item_black_king_bar");

            if (orchid == null)
                orchid = me.FindItem("item_orchid");

            if (abyssalBlade == null)
                abyssalBlade = me.FindItem("item_abyssal_blade");

            var invisModif = me.Modifiers.Any(x => x.Name == "modifier_item_silver_edge_windwalk" || x.Name == "modifier_item_invisibility_edge_windwalk");

            if (Menu.Item("sdtog").GetValue<bool>() && me.IsAlive && me.CanCast() && !me.IsChanneling())
            {
                if (shadowDance != null && shadowDance.IsValid && shadowDance.CanBeCasted() && me.Health <= me.MaximumHealth / 100 * Menu.Item("sdhealth").GetValue<Slider>().Value && Utils.SleepCheck("shadowDance"))
                {
                    shadowDance.UseAbility();
                    Utils.Sleep(200, "shadowDance");
                }
            }

            if (autoKillz && Menu.Item("enable").GetValue<bool>())
            {
                target = me.ClosestToMouseTarget(1001);

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
                    if (me.CanCast() && !me.IsChanneling())
                    {
                        if (!invisModif)
                        {
                        if (!Utils.SleepCheck("attacking"))
                            Orbwalking.Orbwalk(target, Game.Ping);
                            Utils.Sleep(200, "attacking");
                        }

                        if (pounce != null && pounce.CanBeCasted())//change this to leap name fo cause bcz cus cause. D:
                        {
                            if (target.NetworkActivity == NetworkActivity.Move)
                            {
                                var VectorOfMovement = new Vector2((float)Math.Cos(target.RotationRad) * target.MovementSpeed, (float)Math.Sin(target.RotationRad) * target.MovementSpeed);
                                var HitPosition = Interception(target.Position, VectorOfMovement, me.Position, 933.33f);
                                var HitPosMod = HitPosition + new Vector3(VectorOfMovement.X * (TimeToTurn(me, HitPosition)), VectorOfMovement.Y * (TimeToTurn(me, HitPosition)), 0);
                                var HitPosMod2 = HitPosition + new Vector3(VectorOfMovement.X * (TimeToTurn(me, HitPosMod)), VectorOfMovement.Y * (TimeToTurn(me, HitPosMod)), 0);

                                if (GetDistance2D(me, HitPosMod2) > (755 + target.HullRadius))
                                {
                                    return;
                                }
                                if (IsFacing(me, HitPosMod2))
                                {
                                    pounce.UseAbility();
                                    trying = true;
                                    await Task.Delay(400); //Avoid trying to pounce multiple times.
                                    trying = false;
                                }
                                else
                                {
                                    me.Move((HitPosMod2 - me.Position) * 50 / (float)GetDistance2D(HitPosMod2, me) + me.Position);
                                }
                            }
                            else
                            {
                                if (GetDistance2D(me, target) > (755 + target.HullRadius))
                                {
                                    return;
                                }
                                if (IsFacing(me, target))
                                {
                                    pounce.UseAbility();
                                    trying = true;
                                    await Task.Delay(400);
                                    trying = false;
                                }
                                else
                                {
                                    me.Move((target.Position - me.Position) * 50 / (float)GetDistance2D(target, me) + me.Position);
                                }
                            }
                        }

                        if (darkPact != null && darkPact.CanBeCasted() && darkPact.CanHit(target) && Utils.SleepCheck("darkPact"))
                        {
                            darkPact.UseAbility();
                            Utils.Sleep(200, "darkPact");
                        }

                        if (bkb != null && bkb.IsValid && bkb.CanBeCasted() && GetDistance2D(me, target) <= 300 && Utils.SleepCheck("bkb"))
                        {
                            bkb.UseAbility();
                            Utils.Sleep(150 + Game.Ping, "bkb");
                        }

                        if (abyssalBlade != null && abyssalBlade.IsValid && abyssalBlade.CanBeCasted() && Utils.SleepCheck("abyssalBlade"))
                        {
                            abyssalBlade.CastStun(target);
                            Utils.Sleep(250 + Game.Ping, "abyssalBlade");
                        }

                        if (orchid != null && orchid.IsValid && orchid.CanBeCasted() && Utils.SleepCheck("orchid"))
                        {
                            orchid.CastStun(target);
                            Utils.Sleep(250 + Game.Ping, "orchid");
                        }

                    }
                }
            }
        }

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
            }
        }//game_onWndProc

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
            if (player == null || player.Team == Team.Observer || me.ClassID != ClassID.CDOTA_Unit_Hero_Slark)
            {
                return;
            }

            if (Menu.Item("comboKey").GetValue<KeyBind>().Active)
            {
                DrawBox(2, 45, 115, 20, 1, new ColorBGRA(0, 128, 0, 128));
                DrawFilledBox(2, 45, 115, 20, new ColorBGRA(0, 0, 0, 100));
                DrawShadowText(" Slark#: Comboing!", 2, 45, Color.LightBlue, text);
            }
        }

        private static double GetDistance2D(dynamic A, dynamic B)
        {
            if (!(A is Unit || A is Vector3)) throw new ArgumentException("Not valid parameters, accepts Unit|Vector3 only", "A");
            if (!(B is Unit || B is Vector3)) throw new ArgumentException("Not valid parameters, accepts Unit|Vector3 only", "B");
            if (A is Unit) A = A.Position;
            if (B is Unit) B = B.Position;

            return Math.Sqrt(Math.Pow(A.X - B.X, 2) + Math.Pow(A.Y - B.Y, 2));
        }

        private static Vector3 Interception(Vector3 x, Vector2 y, Vector3 z, float s)
        {
            float x1 = x.X - z.X;
            float y1 = x.Y - z.Y;

            float hs = y.X * y.X + y.Y * y.Y - s * s;
            float h1 = x1 * y.X + y1 * y.Y;
            float t;

            if (hs == 0)
            {
                t = -(x1 * x1 + y1 * y1) / 2 * h1;
            }
            else
            {
                float mp = -h1 / hs;
                float d = mp * mp - (x1 * x1 + y1 * y1) / hs;

                float root = (float)Math.Sqrt(d);

                float t1 = mp + root;
                float t2 = mp - root;

                float tMin = Math.Min(t1, t2);
                float tMax = Math.Max(t1, t2);

                t = tMin > 0 ? tMin : tMax;
            }
            return new Vector3(x.X + t * y.X, x.Y + t * y.Y, x.Z);
        }

        private static bool IsFacing(Unit StartUnit, dynamic Target)
        {
            if (!(Target is Unit || Target is Vector3)) throw new ArgumentException("TimeToTurn => INVALID PARAMETERS!", "Target");
            if (Target is Unit) Target = Target.Position;

            float deltaY = StartUnit.Position.Y - Target.Y;
            float deltaX = StartUnit.Position.X - Target.X;
            float angle = (float)(Math.Atan2(deltaY, deltaX));

            float n1 = (float)Math.Sin(StartUnit.RotationRad - angle);
            float n2 = (float)Math.Cos(StartUnit.RotationRad - angle);

            return (Math.PI - Math.Abs(Math.Atan2(n1, n2))) < 0.1;
        }

        private static float TimeToTurn(Unit StartUnit, dynamic Target)
        {
            if (!(Target is Unit || Target is Vector3)) throw new ArgumentException("TimeToTurn => INVALID PARAMETERS!", "Target");
            if (Target is Unit) Target = Target.Position;

            double TurnRate = 0.5; //Game.FindKeyValues(string.Format("{0}/MovementTurnRate", StartUnit.Name), KeyValueSource.Hero).FloatValue; // (Only works in lobby)

            float deltaY = StartUnit.Position.Y - Target.Y;
            float deltaX = StartUnit.Position.X - Target.X;
            float angle = (float)(Math.Atan2(deltaY, deltaX));

            float n1 = (float)Math.Sin(StartUnit.RotationRad - angle);
            float n2 = (float)Math.Cos(StartUnit.RotationRad - angle);

            float Calc = (float)(Math.PI - Math.Abs(Math.Atan2(n1, n2)));

            if (Calc < 0.1 && Calc > -0.1) return 0;

            return (float)(Calc * (0.03 / TurnRate));
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            if (!Game.IsInGame || Game.IsPaused || Game.IsWatchingGame) return;

            me = ObjectManager.LocalHero;
            if (me == null || me.ClassID != ClassID.CDOTA_Unit_Hero_Slark)
                return;

            if (Menu.Item("comboKey").GetValue<KeyBind>().Active)
            {
                if (target == null || !target.IsAlive)
                {
                    return;
                }
                var pos = Drawing.WorldToScreen(target.Position);
                Drawing.DrawText("Target", pos, new Vector2(0, 50), Color.Red, FontFlags.AntiAlias | FontFlags.DropShadow);
            }

        }

        private static void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            text.Dispose();
            notice.Dispose();
            line.Dispose();
        }
    }
  }//END