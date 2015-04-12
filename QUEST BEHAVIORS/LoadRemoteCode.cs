// File: LoadRemoteCode.cs
#region Usings

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using Buddy.Coroutines;

using CommonBehaviors.Actions;

using Styx.CommonBot.Profiles;
using Styx.TreeSharp;

#endregion

/* Place tag as one of the first in the QuestOrder.
 * It will look for RemoteCode behaviors and load a
 * profile based off of the code from the urls.
 */

/* Example usage:
 * <CustomBehavior File="LoadRemoteCode"/>
 */
namespace Pookthetook.QuestBehaviors
{
    /// <summary>
    /// The load remote code quest behavior.
    /// </summary>
    [CustomBehaviorFileName("LoadRemoteCode")]
    public class LoadRemoteCode : CustomForcedBehavior
    {
        /// <summary>
        /// The is behavior done.
        /// </summary>
        private bool isBehaviorDone;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoadRemoteCode"/> class. 
        /// <markup>
        /// <include item="SMCAutoDocConstructor">
        /// <parameter>Styx.CommonBot.Profiles.CustomForcedBehavior</parameter>
        /// </include>
        /// </markup>
        /// </summary>
        /// <param name="args">
        /// The args.
        /// </param>
        public LoadRemoteCode(Dictionary<string, string> args)
            : base(args)
        {
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
                return this.isBehaviorDone;
            }
        }

        /// <summary>
        /// The create behavior composite.
        /// </summary>
        /// <returns>
        /// The <see cref="Composite"/>.
        /// </returns>
        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(coroutine => this.MainCoroutine());
        }

        /// <summary>
        /// Gets a new profile with the remote code loaded.
        /// </summary>
        /// <param name="currentProfile">
        /// The current profile.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/> containing the new profile.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// currentProfile is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// currentProfile does not contain a root element.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Failed to create new profile.
        /// </exception>
        private async Task<XDocument> GetNewProfile(XDocument currentProfile)
        {
            if (currentProfile == null) throw new ArgumentNullException("currentProfile");
            if (currentProfile.Root == null) throw new ArgumentException("currentProfile does not contain a root element.");
            Contract.EndContractBlock();

            try
            {
                var profileCode = await this.GetElementCode(currentProfile.Root); 
                var newProfile = XDocument.Parse(profileCode);
                return newProfile;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to create new profile.", ex);
            }
        }

        /// <summary>
        /// Gets all of the code for a parent element.
        /// </summary>
        /// <param name="parentElement">
        /// The parent element.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/> containing the string of code.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// parentElement is null.
        /// </exception>
        private async Task<string> GetElementCode(XElement parentElement)
        {
            if (parentElement == null) throw new ArgumentNullException("parentElement");
            Contract.EndContractBlock();

            var attributeBuilder = new StringBuilder();
            if (parentElement.HasAttributes)
            {
                foreach (var attribute in parentElement.Attributes())
                {
                    attributeBuilder.Append(attribute + " ");
                }
            }

            var profileBuilder = new StringBuilder();
            profileBuilder.AppendFormat("<{0} {1}>\n", parentElement.Name, attributeBuilder);

            foreach (var element in parentElement.Elements())
            {
                if (element.HasElements)
                {
                    var childCode = await this.GetElementCode(element);
                    profileBuilder.AppendLine(childCode);
                }
                else if (element.Name == "CustomBehavior" && element.Attribute("File") != null && element.Attribute("File")
                                                                                                         .Value == "LoadRemoteCode")
                {
                    // Ignore so that it won't try to load again.
                }
                else if (element.Name == "CustomBehavior" && element.Attribute("File") != null && element.Attribute("File")
                                                                                                         .Value == "RemoteCode")
                {
                    var remoteCode = await this.GetRemoteCode(element);
                    profileBuilder.AppendLine(remoteCode);
                }
                else
                {
                    profileBuilder.AppendLine(element.ToString());
                }
            }

            profileBuilder.AppendFormat("</{0}>\n", parentElement.Name);

            return profileBuilder.ToString();
        }

        /// <summary>
        /// Gets remote code from a server.
        /// </summary>
        /// <param name="remoteCodeElement">
        /// The remote code element containing a CodeUrl.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/> returning the remote code.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// remoteCodeElement is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// RemoteCode element is invalid.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Failed to load code.
        /// </exception>
        private async Task<string> GetRemoteCode(XElement remoteCodeElement)
        {
            if (remoteCodeElement == null) throw new ArgumentNullException("remoteCodeElement");
            if (remoteCodeElement.Attribute("File") == null) throw new ArgumentException("CustomBehavior RemoteCode element does not contain a File attribute.");
            if (remoteCodeElement.Attribute("File").Value != "RemoteCode") throw new ArgumentException("CustomBehavior RemoteCode element File attribute is not RemoteCode.");
            if (remoteCodeElement.Attribute("CodeUrl") == null) throw new ArgumentException("CustomBehavior RemoteCode element does not contain a CodeUrl attribute.");
            if (string.IsNullOrWhiteSpace(remoteCodeElement.Attribute("CodeUrl").Value)) throw new ArgumentException("CustomBehavior RemoteCode element CodeUrl attribute is empty.");
            Contract.EndContractBlock();

            var codeUrl = remoteCodeElement.Attribute("CodeUrl")
                                           .Value;
            string codeString;
            using (var client = new WebClient())
            {
                try
                {
                    this.LogMessage("info", string.Format("Loading remote code from {0}.", codeUrl));
                    codeString = await Coroutine.ExternalTask(client.DownloadStringTaskAsync(new Uri(codeUrl)));
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(string.Format("Failed to load remote code from {0}.", codeUrl), ex);
                }
            }

            try
            {
                // Make sure it's formatted correctly
                var stringBuilder = new StringBuilder(
                    XDocument.Parse(codeString)
                             .ToString());
                stringBuilder.Replace("<HBProfile>", string.Empty);
                stringBuilder.Replace("</HBProfile>", string.Empty);
                stringBuilder.Replace("<QuestOrder>", string.Empty);
                stringBuilder.Replace("</QuestOrder>", string.Empty);
                codeString = stringBuilder.ToString();
            }
            catch (Exception)
            {
                // Profile can't be parsed into a document so it's just code.
            }

            return codeString;
        }

        /// <summary>
        /// Gets the XDocument for the current profile.
        /// </summary>
        /// <returns>
        /// The <see cref="XDocument"/> containing the current profile.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Profile could not be loaded.
        /// </exception>
        private XDocument GetProfileXDocument()
        {
            var path = ProfileManager.XmlLocation;
            try
            {
                XDocument profile;
                using (var streamReader = new StreamReader(path, Encoding.UTF8, true))
                {
                    profile = XDocument.Load(streamReader, LoadOptions.PreserveWhitespace);
                }

                return profile;
            }
            catch (FileNotFoundException ex)
            {
                throw new InvalidOperationException(string.Format("Profile could not be found at {0}.", path), ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new InvalidOperationException(string.Format("Directory for profile could not be found at {0}.", path), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format("Failed to get profile from {0}.", path), ex);
            }
        }

        /// <summary>
        /// The main coroutine.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        private async Task<bool> MainCoroutine()
        {
            if (this.IsDone) return true;

            this.LogMessage("info", "Getting new profile.");

            try
            {
                var currentProfile = this.GetProfileXDocument();
                var newProfile = await this.GetNewProfile(currentProfile);

                using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(newProfile.ToString())))
                {
                    this.LogMessage("info", "Loading new profile.");
                    ProfileManager.LoadNew(memoryStream);
                    await Coroutine.Sleep(300);
                }
            }
            catch (Exception ex)
            {
                this.LogMessage("fatal", string.Format("LoadRemoteCode Failed.\n{0}", ex));
            }
            
            this.isBehaviorDone = true;
            return true;
        }
    }
}