// $Id$
using System;

using NSvn.Core;
using NSvn.Common;
using EnvDTE;
using Ankh.UI;
using System.Text;
using System.Windows.Forms;
using System.Collections;

namespace Ankh.Commands
{
    /// <summary>
    /// Summary description for RevertItem.
    /// </summary>

    [VSNetCommand("RevertItem", Text = "Revert...", Tooltip = "Reverts selected item",
         Bitmap = ResourceBitmaps.Revert),
    VSNetControl( "Item.Ankh", Position = 1 ),
    VSNetProjectNodeControl( "Ankh", Position = 1 ),
    VSNetFolderNodeControl( "Ankh", Position = 1),
    VSNetControl( "Solution.Ankh", Position = 1)]
    public class RevertItemCommand : CommandBase
    {
        #region Implementation of ICommand

        public override EnvDTE.vsCommandStatus QueryStatus(Ankh.IContext context)
        {   
            if ( context.SolutionExplorer.GetSelectionResources( true, 
                new ResourceFilterCallback( CommandBase.ModifiedFilter ) ).Count > 0 )
            {
                return Enabled;
            }
            else
                return Disabled;
        }

        public override void Execute(Ankh.IContext context, string parameters)
        {
            this.SaveAllDirtyDocuments( context );

            // get the modified resources
            IList resources = context.SolutionExplorer.GetSelectionResources( true,
                new ResourceFilterCallback( CommandBase.ModifiedFilter ) );

            bool recursive = false;
            bool confirmed = false;
            // is Shift down?
            if ( !CommandBase.Shift )
            {
                PathSelectorInfo info = new PathSelectorInfo( "Select items to revert", 
                    resources, resources );
                info = context.UIShell.ShowPathSelector( info );
                if ( info == null )
                    return;
                confirmed = true;
                recursive = info.Recursive;
                resources = info.CheckedItems;                
            }

            string[] paths = SvnItem.GetPaths( resources );
            
            // ask for confirmation if the Shift dialog hasn't been used
            if ( !confirmed )
            {
                string msg = "Do you really want to revert these item(s)?" + 
                    Environment.NewLine + Environment.NewLine;            
                msg += string.Join( Environment.NewLine, paths );

                if( MessageBox.Show( context.HostWindow, msg, "Revert", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information ) != DialogResult.Yes )
                {
                    return;
                }
            }
               
            // perform the actual revert 
            context.OutputPane.StartActionText("Reverting");  
            context.ProjectFileWatcher.StartWatchingForChanges(); 
            try
            {
                context.Client.Revert( paths, recursive );
            }
            catch( NotVersionControlledException )
            {
                // empty
            }

            if ( !context.ReloadSolutionIfNecessary() )
            {
                foreach( SvnItem item in resources )
                    item.Refresh( context.Client );
            }

            context.OutputPane.EndActionText();
        }               
        #endregion       
    }
}



