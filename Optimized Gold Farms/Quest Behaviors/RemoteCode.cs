// File: RemoteCode.cs
#region Usings

using System;
using System.Collections.Generic;

using CommonBehaviors.Actions;

using Styx.CommonBot.Profiles;
using Styx.TreeSharp;

#endregion

/* Does nothing on it's own.
 * To be used with LoadRemoteCode behavior.
 */

/* Example usage:
 * <CustomBehavior File="RemoteCode" CodeUrl="http://pookthetook.com/uploadedfiles/hello.xml"/>
 */
namespace Pookthetook.QuestBehaviors
{
    /// <summary>
    /// The remote code quest behavior.
    /// </summary>
    [CustomBehaviorFileName("RemoteCode")]
    public class RemoteCode : CustomForcedBehavior
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteCode"/> class.
        /// </summary>
        /// <param name="args">
        /// The args.
        /// </param>
        public RemoteCode(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                var codeUrl = this.GetAttributeAs("CodeUrl", true, ConstrainAs.StringNonEmpty, null) ?? string.Empty;
            }
            catch (Exception ex)
            {
                this.LogMessage("error", string.Format("BEHAVIOR PROBLEM: {0}\nFROM:\n{1}\n", ex.Message, ex.StackTrace));
                this.IsAttributeProblem = true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this object is done.
        /// </summary>
        /// <value>
        /// true if this object is done, false if not.
        /// </value>
        public override bool IsDone
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// The create behavior composite.
        /// </summary>
        /// <returns>
        /// The <see cref="Composite" />.
        /// </returns>
        protected override Composite CreateBehavior()
        {
            return new ActionAlwaysSucceed();
        }
    }
}