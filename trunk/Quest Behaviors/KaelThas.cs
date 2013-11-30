// Behavior To complete Kael'Thas Encounter in Tempest Keep: The Eye
// ==
// DOCUMENTATION:
//  In profile, the location at which each of Kael'Thas' 4 adds can be fought using the following tunables:
// ThaladredFight X,Y,Z
// SanguinarFight X,Y,Z
//CapernianFight X,Y,Z
//TellonicusFight X,Y,Z
//
// Example:
//<CustomBehavior File="KaelThas" BossId="19622" AddId1="21270" AddId2="21269" AddId3="21268" AddId4="21271" AddId5="21272" AddId6="21273" AddId7="21274" 
//							ThaladredFightX="647.8159" ThaladredFightY="33.54127" ThaladredFightZ="46.77886"
//							SanguinarFightX="710.8145" SanguinarFightY="-69.7039" SanguinarFightZ="46.7789"
//							CapernianFightX="789.914" CapernianFightY="-45.44182" CapernianFightZ="46.77895"
//							TellonicusFightX="810.4791" TellonicusFightY="41.20256" TellonicusFightZ="46.77884" />

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;

using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;
using Tripper.Tools.Math;
using Styx.WoWInternals.World;








using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.KaelThas
{
    [CustomBehaviorFileName(@"KaelThas")]
    public class Kael : CustomForcedBehavior
    {
        /// <summary>
        /// This is only used when you get a quest that Says, Kill anything x times. Or on the chance the wowhead ID is wrong
        /// ##Syntax##
        /// AddIdN: Ids of the adds involved in the fight
        /// BossId: Id of the "Boss", or immune target that is being switched from
        /// X,Y,Z: The general location where theese objects can be found
        /// AuraId: Aura that causes us to switch from the boss to the adds
        /// </summary>
        /// 
        public Kael(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Location = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                AuraId = GetAttributeAsNullable<int>("AuraId", false, ConstrainAs.MobId, new[] { "NpcId", "NpcID" }) ?? 0;
                BossId = GetAttributeAsNullable<int>("BossId", false, ConstrainAs.MobId, new[] { "NpcId", "NpcID" }) ?? 0;
                AddIds = GetNumberedAttributesAsArray<int>("AddId", 0, ConstrainAs.MobId, new[] { "NpcId" });
                ThaladredLoc = GetAttributeAsNullable<WoWPoint>("ThaladredFight", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                SanguinarLoc = GetAttributeAsNullable<WoWPoint>("SanguinarFight", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                CapernianLoc = GetAttributeAsNullable<WoWPoint>("CapernianFight", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                TellonicusLoc = GetAttributeAsNullable<WoWPoint>("TellonicusFight", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;

            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error",
                           "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace +
                           "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public WoWPoint Location { get; private set; }
        public WoWPoint ThaladredLoc { get; private set; }
        public WoWPoint SanguinarLoc { get; private set; }
        public WoWPoint CapernianLoc { get; private set; }
        public WoWPoint TellonicusLoc { get; private set; }
        public bool WeaponsDown = false;
        private int[] AddIds { get; set; }
        private int BossId { get; set; }
        private int AuraId { get; set; }


        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        // Private properties
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }




        private WoWUnit Weapons
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Attackable && !u.Lootable && AddIds.Contains((int)u.Entry)).OrderBy(
                        u => u.Distance).FirstOrDefault());
            }
        }

        private List<WoWUnit> KaelthasMinions
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => (u.Entry == 20063 || u.Entry == 20064 || u.Entry == 20060 || u.Entry == 20062) && u.IsAlive).OrderBy(
                        u => u.Distance).ToList());
            }
        }

        public List<WoWUnit> NetherVapors
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 21002 && u.DistanceSqr < 5f * 5f).OrderBy(
                        u => u.Distance).ToList());
            }
        }

        private WoWUnit Boss
        {
            get { return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == BossId)); }
        }

        private WoWUnit Tellonicus
        {
            get { return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 20063)); }
        }

        private WoWUnit Thaladred
        {
            get { return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 20064)); }
        }

        private WoWUnit Sanguinar
        {
            get { return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 20060)); }
        }

        private WoWUnit Capernian
        {
            get { return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 20062)); }
        }

        private bool NetherMove()
        {
            foreach (WoWUnit NetherVapor in NetherVapors)
            {
                if (NetherVapor.Location.Distance(StyxWoW.Me.Location) < 5)
                {
                    WoWMovement.ClickToMove(WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, NetherVapor.Location, 20f));
                    return true;
                }
                if (Boss.Location.Distance(StyxWoW.Me.Location) > 3)
                {
                    WoWMovement.ClickToMove(Boss.Location);
                    return true;
                }
            }
            return false;
        }


        ~Kael()
        {
            Dispose(false);
        }




        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    // empty, for now
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }




        #region Overrides of CustomForcedBehavior



        public bool IsMobDead()
        {
            var interestingmobs = ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == BossId && !u.IsDead && !u.IsFriendly).OrderBy(
                        u => u.Distance).FirstOrDefault();
            return interestingmobs == null;
        }

        public Composite DoneYet
        {
            get
            {
                return
                    new Decorator(ret => IsMobDead() && !Me.Combat, new Action(delegate
                    {

                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }

        public Composite DoDps
        {
            get
            {
                return new
                    PrioritySelector(RoutineManager.Current.CombatBuffBehavior,
                                            RoutineManager.Current.CombatBehavior);
            }
        }



        public Composite KillTellonicus
        {
            get
            {
                return new Decorator(r => Tellonicus.MaxHealth < 200000 && Weapons == null && Tellonicus.Attackable && Tellonicus != null && !Tellonicus.Stunned && Tellonicus.IsAlive, new PrioritySelector(
                                                                                                                               new Decorator(r => TellonicusLoc.Distance(StyxWoW.Me.Location) >= 2,
                                                                                                                                            new Action(r => Navigator.MoveTo(TellonicusLoc))),
                                                                                                                               new Decorator(r => Me.CurrentTarget == null || Me.CurrentTarget != Tellonicus,
                                                                                                                                            new Action(r => Tellonicus.Target())),
                                                                                                                                            DoDps
                                                                             )


                    );
            }
        }

        public Composite KillCapernian
        {
            get
            {
                return new Decorator(r => Capernian.MaxHealth < 200000 && Capernian.Attackable && Capernian != null && !Capernian.Stunned && Capernian.IsAlive, new PrioritySelector(
                                                                                                                               new Decorator(r => (CapernianLoc.Distance(StyxWoW.Me.Location)) >= 35,
                                                                                                                                            new Action(r => Navigator.MoveTo(CapernianLoc))),
                                                                                                                               new Decorator(r => Me.CurrentTarget == null || Me.CurrentTarget != Capernian,
                                                                                                                                            new Action(r => Capernian.Target())),
                                                                                                                                            DoDps
                                                                             )


                    );
            }
        }

        public Composite KillSanguinar
        {
            get
            {
                return new Decorator(r => Sanguinar.MaxHealth < 200000 && Sanguinar.Attackable && Sanguinar != null && !Sanguinar.Stunned && Sanguinar.IsAlive, new PrioritySelector(
                                                                                                                               new Decorator(r => SanguinarLoc.Distance(StyxWoW.Me.Location) >= 2,
                                                                                                                                            new Action(r => Navigator.MoveTo(SanguinarLoc))),
                                                                                                                               new Decorator(r => Me.CurrentTarget == null || Me.CurrentTarget != Sanguinar,
                                                                                                                                            new Action(r => Sanguinar.Target())),
                                                                                                                                            DoDps
                                                                             )


                    );
            }
        }

        public Composite KillThaladred
        {
            get
            {
                return new Decorator(r => Thaladred.MaxHealth < 200000 && Thaladred.Attackable && Thaladred != null && !Thaladred.Stunned && Thaladred.IsAlive, new PrioritySelector(
                                                                                                                               new Decorator(r => ThaladredLoc.Distance(StyxWoW.Me.Location) >= 2,
                                                                                                                                            new Action(r => Navigator.MoveTo(ThaladredLoc))),
                                                                                                                               new Decorator(r => Me.CurrentTarget == null || Me.CurrentTarget != Thaladred,
                                                                                                                                            new Action(r => Thaladred.Target())),
                                                                                                                                            DoDps
                                                                             )


                    );
            }
        }

        public Composite KillWeapons
        {
            get
            {
                return new Decorator(r => Weapons != null, new PrioritySelector(

                    new Decorator(r => TellonicusLoc.Distance(StyxWoW.Me.Location) > 2, new Action(r => Navigator.MoveTo(TellonicusLoc))),
                    new Decorator(r => !Me.GotTarget || Me.CurrentTarget != Weapons, new Action(r => Weapons.Target())),
                    DoDps




                    ));
            }
        }

        public Composite AddZerg
        {
            get
            {
                return new Decorator(r => KaelthasMinions.Count > 1 && (Tellonicus.MaxHealth > 200000 || Capernian.MaxHealth > 200000 || Sanguinar.MaxHealth > 200000 || Thaladred.MaxHealth > 200000), new PrioritySelector(

                    new Decorator(r => TellonicusLoc.Distance(StyxWoW.Me.Location) >= 2, new Action(r => Navigator.MoveTo(TellonicusLoc))),
                    new Decorator(r => !Me.GotTarget || (Me.CurrentTarget != Tellonicus && Tellonicus.HealthPercent > 1 && Tellonicus != null), new Action(r => Tellonicus.Target())),
                    new Decorator(r => Me.CurrentTarget == Tellonicus && Tellonicus.HealthPercent > 1 && Tellonicus != null, DoDps),
                    new Decorator(r => !Me.GotTarget || (Me.CurrentTarget != Sanguinar && Sanguinar.HealthPercent > 1 && Sanguinar != null), new Action(r => Sanguinar.Target())),
                    new Decorator(r => Me.CurrentTarget == Sanguinar && Sanguinar.HealthPercent > 1 && Sanguinar != null, DoDps),
                    new Decorator(r => !Me.GotTarget || (Me.CurrentTarget != Thaladred && Thaladred.HealthPercent > 1 && Thaladred != null), new Action(r => Thaladred.Target())),
                    new Decorator(r => Me.CurrentTarget == Thaladred && Thaladred.HealthPercent > 1 && Thaladred != null, DoDps)




                    ));
            }
        }

        public Composite FinishAdds
        {
            get
            {

                return new Decorator(r => Capernian.MaxHealth > 200000 && !Sanguinar.IsAlive && !Thaladred.IsAlive && Capernian.IsAlive, new PrioritySelector(
                                                                            new Decorator(r => !Me.GotTarget || (Me.CurrentTarget != Capernian && Capernian.HealthPercent > 1 && Capernian != null), new Action(r => Capernian.Target())),
                                                                            new Decorator(r => Me.CurrentTarget == Capernian && Capernian.IsAlive && Capernian != null, DoDps)
                                                                            ));
            }
        }

        public Composite KillBoss
        {
            get
            {
                return new Decorator(r => Boss != null && Boss.Attackable, new PrioritySelector(


                    new Decorator(r => !Me.GotTarget || Me.CurrentTarget != Boss, new Action(r => Boss.Target())),
                    new Decorator(r => NetherVapors.Any(), new Action(delegate
                {
                    NetherMove();
                    return RunStatus.Success;
                })),
                    DoDps



                    ));
            }
        }


        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, AddZerg, FinishAdds, KillCapernian, KillTellonicus, KillSanguinar, KillThaladred, KillWeapons, KillBoss)));
        }


        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone);
            }
        }


        public override void OnStart()
        {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {

                if (TreeRoot.Current != null && TreeRoot.Current.Root != null &&
    TreeRoot.Current.Root.LastStatus != RunStatus.Running)
                {
                    var currentRoot = TreeRoot.Current.Root;
                    if (currentRoot is GroupComposite)
                    {
                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, CreateBehavior());
                    }
                }




        #endregion
            }
        }
    }
}