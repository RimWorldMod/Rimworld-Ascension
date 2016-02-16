﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace RA
{
    public class Shield : Apparel
    {
        public static readonly SoundDef SoundAbsorbDamage = SoundDef.Named("PersonalShieldAbsorbDamage");
        public static readonly SoundDef SoundBreak = SoundDef.Named("PersonalShieldBroken");

        public const int BaseAbsorbChance_Melee = 25;
        public const int BaseAbsorbChance_Ranged = 50;

        // determine when to display shield texture and gizmo
        public bool ShouldDisplay
        {
            get
            {
                return !this.wearer.Dead && !this.wearer.Downed && this.wearer.Faction.HostileTo(Faction.OfColony) && (!this.wearer.IsPrisonerOfColony || (this.wearer.BrokenStateDef != null && this.wearer.BrokenStateDef.isAggro));
            }
        }

        // absorbs recieved damage
        public override bool CheckPreAbsorbDamage(DamageInfo dinfo)
        {
            if (dinfo.Instigator != null)
            {
                SkillRecord meleeSkill = this.wearer.skills.GetSkill(SkillDefOf.Melee);
                float hitRoll = new System.Random().Next(1, 100);

                // instigator is melee and damage not explosive
                if (dinfo.Instigator.Position.AdjacentTo8WayOrInside(this.wearer.Position) && !dinfo.Def.isExplosive)
                {
                    if (hitRoll <= BaseAbsorbChance_Melee + meleeSkill.level * 2)
                    {
                        return TryAbsorbDamage(dinfo);
                    }
                }

                // instigator is ranged and damage not explosive
                if (!dinfo.Instigator.Position.AdjacentTo8WayOrInside(this.wearer.Position) && !dinfo.Def.isExplosive)
                {
                    if (hitRoll <= BaseAbsorbChance_Ranged + meleeSkill.level * 2)
                    {
                        return TryAbsorbDamage(dinfo);
                    }
                }
            }

            return false;
        }

        // returns true if damage is completely absorbed
        public bool TryAbsorbDamage(DamageInfo dinfo)
        {
            this.HitPoints -= dinfo.Amount;

            if (this.HitPoints <= 0)
            {
                dinfo.SetAmount(dinfo.Amount + this.HitPoints);
                Break();
                return false;
            }
            else
            {
                ThrowAbsorbationEffects(dinfo);
                return true;
            }
        }

        // throws special effects when damage is absorved
        public void ThrowAbsorbationEffects(DamageInfo dinfo)
        {
            SoundAbsorbDamage.PlayOneShot(this.wearer.Position);
            Vector3 loc = this.wearer.TrueCenter() + Vector3Utility.HorizontalVectorFromAngle(dinfo.Angle).RotatedBy(180f) * 0.5f;
            MoteThrower.ThrowStatic(loc, ThingDefOf.Mote_ShotHit_Spark, 1f);
            MoteThrower.ThrowText(loc, "Blocked");
        }

        public void Break()
        {
            SoundBreak.PlayOneShot(this.wearer.Position);
            this.Destroy();
        }

        // allows to attack only adjacent cells (melee targets)
        public override bool AllowVerbCast(IntVec3 root, TargetInfo targ)
        {
            if (targ.HasThing && targ.Thing.def.size != IntVec2.One)
            {
                return root.AdjacentTo8WayOrInside(targ.Thing);
            }
            return root.AdjacentTo8Way(targ.Cell);
        }

        // draws thing on pawn
        public override void DrawWornExtras()
        {
            float turnAngle = 0f;
            Vector3 drawCenter = this.wearer.drawer.DrawPos;
            drawCenter.y = Altitudes.AltitudeFor(AltitudeLayer.Pawn);
            Vector3 s = new Vector3(1f, 1f, 1f);

            if (this.wearer.Rotation == Rot4.North && (this.wearer.Faction == Faction.OfColony && !this.wearer.Drafted || this.wearer.Faction.HostileTo(Faction.OfColony)))
            {
                drawCenter.y += 0.1f;
            }

            if (this.wearer.Rotation == Rot4.South)
            {
                drawCenter.y += 0.1f;
                drawCenter.x += 0.2f;
                drawCenter.z -= 0.2f;
            }

            if (this.wearer.Rotation == Rot4.East)
            {
                drawCenter.y -= 0.1f;
                drawCenter.z -= 0.2f;
                turnAngle = 60f;
            }

            if (this.wearer.Rotation == Rot4.West)
            {
                drawCenter.y += 0.1f;
                drawCenter.z -= 0.2f;
                turnAngle = 300f;
            }

            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(drawCenter, Quaternion.AngleAxis(turnAngle, Vector3.up), s);
            Graphics.DrawMesh(MeshPool.plane10, matrix, this.Graphic.MatSingle, 0);
        }

        // draws gizmo with shield durability
        public override IEnumerable<Gizmo> GetWornGizmos()
        {
            base.GetWornGizmos();

            Gizmo_ShieldStatus gizmoShield = new Gizmo_ShieldStatus
            {
                shield = this
            };
            yield return gizmoShield;
        }
    }

    public class Gizmo_ShieldStatus : Gizmo
    {
        public static readonly Texture2D FullTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.24f));
        public static readonly Texture2D EmptyTex = SolidColorMaterials.NewSolidColorTexture(Color.clear);
        public static readonly Texture2D meleeBlock_Icon = ContentFinder<Texture2D>.Get("UI/Icons/MeleeBlock", true);
        public static readonly Texture2D rangedBlock_Icon = ContentFinder<Texture2D>.Get("UI/Icons/RangedBlock", true);

        public const int ShieldIcon_Size = 75;

        public Shield shield;

        public override float Width
        {
            get
            {
                return 140f;
            }
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft)
        {
            Rect gizmoRect = new Rect(topLeft.x, topLeft.y, this.Width, Height);
            Widgets.DrawWindowBackground(gizmoRect);

            Rect gizmoRect_margined = gizmoRect.ContractedBy(5f);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            // header text
            Rect headerTextRect = gizmoRect_margined;
            headerTextRect.height = gizmoRect_margined.height / 3;
            headerTextRect.width = headerTextRect.height + 40;
            Widgets.Label(headerTextRect, "Block:");

            // melee block icon
            // NOTE: mouse over texture shows description
            //TooltipHandler.TipRegion(buttonRect, "gdfgdfgdfgdfgdfgf");
            Rect meleeBlock_IconRect = headerTextRect;
            meleeBlock_IconRect.y += headerTextRect.height;
            meleeBlock_IconRect.width = meleeBlock_IconRect.height;
            Widgets.DrawTextureFitted(meleeBlock_IconRect, meleeBlock_Icon, 0.9f);

            Text.Anchor = TextAnchor.MiddleLeft;

            // melee block hit chance label
            Rect meleeBlock_LabelRect = meleeBlock_IconRect;
            meleeBlock_LabelRect.x += meleeBlock_IconRect.width;
            meleeBlock_LabelRect.width = 40;
            Widgets.Label(meleeBlock_LabelRect, " " + (25 + shield.wearer.skills.GetSkill(SkillDefOf.Melee).level * 2) + "%");

            // ranged block icon
            Rect rangedBlock_IconRect = meleeBlock_IconRect;
            rangedBlock_IconRect.y += meleeBlock_IconRect.height;
            Widgets.DrawTextureFitted(rangedBlock_IconRect, rangedBlock_Icon, 0.9f);

            // ranged block hit chance label
            Rect rangedBlock_LabelRect = meleeBlock_LabelRect;
            rangedBlock_LabelRect.y += meleeBlock_LabelRect.height;
            Widgets.Label(rangedBlock_LabelRect, " " + (50 + shield.wearer.skills.GetSkill(SkillDefOf.Melee).level * 2) + "%");

            Rect healthMeterRect = rangedBlock_IconRect;
            healthMeterRect.x += rangedBlock_IconRect.width + rangedBlock_LabelRect.width;
            healthMeterRect.width = gizmoRect_margined.xMax - healthMeterRect.x;
            float fillPercent = (float)shield.HitPoints / Mathf.Max(1f, (float)shield.MaxHitPoints);
            // NOTE: Widgets.FillableBarLabeled?
            Widgets.FillableBar(healthMeterRect, fillPercent, FullTex, EmptyTex, false);

            Rect iconRect = new Rect(healthMeterRect.xMax - (healthMeterRect.xMax - healthMeterRect.x) / 2 - ShieldIcon_Size/2, gizmoRect_margined.y - ShieldIcon_Size / 5, ShieldIcon_Size, ShieldIcon_Size);
            Widgets.ThingIcon(iconRect, shield);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(healthMeterRect, shield.HitPoints + " / " + shield.MaxHitPoints);

            Text.Anchor = TextAnchor.UpperLeft;
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
