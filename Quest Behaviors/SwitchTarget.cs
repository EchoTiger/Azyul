// Behavior originally contributed by mastahg Heavily modified by mjj23
// ==
// DOCUMENTATION:
//     
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SwitchTarget
{
    [CustomBehaviorFileName(@"SwitchTarget")]
    public class SwitchTarg : CustomForcedBehavior
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
        public SwitchTarg(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Location = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ??WoWPoint.Empty;
                AuraId = GetAttributeAsNullable<int>("AuraId", false, ConstrainAs.MobId, new[] { "NpcId", "NpcID" }) ?? 0;
				BossId = GetAttributeAsNullable<int>("BossId", false, ConstrainAs.MobId, new[] { "NpcId", "NpcID" }) ?? 0;
				AddIds = GetNumberedAttributesAsArray<int>("AddId", 0, ConstrainAs.MobId, new[] { "NpcId" });
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
		
		

      
        private WoWUnit Adds
        {
            get
            {   return (ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => AddIds.Contains((int)u.Entry) && !u.IsDead).OrderBy(
                        u => u.Distance).FirstOrDefault());
            }
        }
		
		private WoWUnit Boss
        {
            get { return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == BossId)); }
        }


        ~SwitchTarg()
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
                    new Decorator(ret => IsMobDead(), new Action(delegate
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
                return new PrioritySelector(RoutineManager.Current.CombatBuffBehavior,
                                            RoutineManager.Current.CombatBehavior);
            }
        }

       

        public Composite KillAdds
        {
            get
            {
                return new Decorator(r=> Adds != null && Boss.Me.HasAura(AuraId), new PrioritySelector(
                    
                    
                    new Decorator(r=> !Me.GotTarget || Me.CurrentTarget != Adds, new Action(r=>Adds.Target())),
                    DoDps

                    
                    
                    ));
            }
        }


        public Composite KillBoss
        {
            get
            {
                return new Decorator(r => Boss != null && !Boss.Me.HasAura(AuraId), new PrioritySelector(


                    new Decorator(r => !Me.GotTarget || Me.CurrentTarget != Boss, new Action(r => Boss.Target())),
                    DoDps



                    ));
            }
        }


        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone,new PrioritySelector(DoneYet, KillAdds, KillBoss)));
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
}}}