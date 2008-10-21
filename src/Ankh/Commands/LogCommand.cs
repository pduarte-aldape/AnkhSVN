// $Id$
using System;

using System.Diagnostics;
using System.Xml;
using System.Xml.Xsl;
using System.IO;
using System.Collections;
using SharpSvn;
using System.Collections.ObjectModel;
using Ankh.Ids;
using System.Collections.Generic;
using Ankh.UI;
using Ankh.UI.SvnLog;
using Ankh.Selection;
using Ankh.VS;
using Ankh.Scc;
using Ankh.Scc.UI;

namespace Ankh.Commands
{
    /// <summary>
    /// Command to show the change log for the selected item.
    /// </summary>
    [Command(AnkhCommand.Log)]
    [Command(AnkhCommand.ProjectHistory)]
    [Command(AnkhCommand.SolutionHistory)]
    [Command(AnkhCommand.ReposExplorerLog)]
    [Command(AnkhCommand.BlameShowLog)]
    class LogCommand : CommandBase
    {
        public override void OnUpdate(CommandUpdateEventArgs e)
        {
            int i;

            switch (e.Command)
            {
                case AnkhCommand.ProjectHistory:
                    SvnProject p = EnumTools.GetFirst(e.Selection.GetSelectedProjects(false));
                    if (p == null)
                        break;

                    ISvnProjectInfo pi = e.GetService<IProjectFileMapper>().GetProjectInfo(p);

                    if (pi == null || string.IsNullOrEmpty(pi.ProjectDirectory))
                        return; // No project location

                    if (e.GetService<IFileStatusCache>()[pi.ProjectDirectory].HasCopyableHistory)
                        return; // Ok, we have history!                                           

                    break; // No history

                case AnkhCommand.SolutionHistory:
                    IAnkhSolutionSettings ss = e.GetService<IAnkhSolutionSettings>();

                    if (string.IsNullOrEmpty(ss.ProjectRoot))
                        break;

                    if (e.GetService<IFileStatusCache>()[ss.ProjectRoot].HasCopyableHistory)
                        return; // Ok, we have history!

                    break; // No history
                case AnkhCommand.Log:
                    int itemCount = 0;
                    int needsRemoteCount = 0;
                    foreach (SvnItem item in e.Selection.GetSelectedSvnItems(false))
                    {
                        if (!item.IsVersioned)
                        {
                            e.Enabled = false;
                            return;
                        }

                        if (item.IsReplaced || item.IsAdded)
                        {
                            if (item.HasCopyableHistory)
                                needsRemoteCount++;
                            else
                            {
                                e.Enabled = false;
                                return;
                            }
                        }
                        itemCount++;
                    }
                    if (itemCount == 0 || (needsRemoteCount != 0 && itemCount > 1))
                    {
                        e.Enabled = false;
                        return;
                    }
                    if (needsRemoteCount >= 1)
                    {
                        // One remote log
                        Debug.Assert(needsRemoteCount == 1);
                        return;
                    }
                    else
                    {
                        // Local log only
                        return;
                    }

                //break;
                case AnkhCommand.ReposExplorerLog:
                    i = 0;
                    foreach (ISvnRepositoryItem item in e.Selection.GetSelection<ISvnRepositoryItem>())
                    {
                        if (item == null || item.Uri == null)
                            continue;
                        i++;
                        if (i > 1)
                            break;
                    }
                    if (i == 1)
                        return;
                    break;
                case AnkhCommand.BlameShowLog:
                    i = 0;
                    foreach (IAnnotateSection section in e.Selection.GetSelection<IAnnotateSection>())
                    {
                        if (section == null)
                            continue;
                        i++;
                    }

                    if (i == 1)
                        return;
                    break;
            }
            e.Enabled = false;
        }

        public override void OnExecute(CommandEventArgs e)
        {
            List<SvnOrigin> selected = new List<SvnOrigin>();
            IFileStatusCache cache = e.GetService<IFileStatusCache>();

            switch (e.Command)
            {
                case AnkhCommand.Log:
                    IAnkhDiffHandler diffHandler = e.GetService<IAnkhDiffHandler>();
                    List<SvnOrigin> items = new List<SvnOrigin>();
                    foreach (SvnItem i in e.Selection.GetSelectedSvnItems(false))
                    {
                        Debug.Assert(i.IsVersioned);

                        if (i.IsReplaced || i.IsAdded)
                        {
                            if (!i.HasCopyableHistory)
                                continue;

                            items.Add(new SvnOrigin(diffHandler.GetCopyOrigin(i), i.WorkingCopy.RepositoryRoot));
                            continue;
                        }

                        items.Add(new SvnOrigin(i));
                    }
                    PerformLog(e.Context, items, null, null);
                    break;
                case AnkhCommand.SolutionHistory:
                    IAnkhSolutionSettings settings = e.GetService<IAnkhSolutionSettings>();

                    PerformLog(e.Context, new SvnOrigin[] { new SvnOrigin(cache[settings.ProjectRoot]) }, null, null);
                    break;
                case AnkhCommand.ProjectHistory:
                    IProjectFileMapper mapper = e.GetService<IProjectFileMapper>();
                    foreach (SvnProject p in e.Selection.GetSelectedProjects(false))
                    {
                        ISvnProjectInfo info = mapper.GetProjectInfo(p);

                        if (info != null)
                            selected.Add(new SvnOrigin(cache[info.ProjectDirectory]));
                    }

                    PerformLog(e.Context, selected, null, null);
                    break;
                case AnkhCommand.ReposExplorerLog:
                    ISvnRepositoryItem item = null;
                    foreach (ISvnRepositoryItem i in e.Selection.GetSelection<ISvnRepositoryItem>())
                    {
                        if (i != null && i.Uri != null)
                            item = i;
                        break;
                    }

                    if (item != null)
                        PerformLog(e.Context, new SvnOrigin[] { item.Origin }, null, null);
                    break;
                case AnkhCommand.BlameShowLog:
                    IAnnotateSection section = null;
                    foreach (IAnnotateSection s in e.Selection.GetSelection<IAnnotateSection>())
                    {
                        section = s;
                        break;
                    }
                    if (section == null)
                        return;

                    PerformLog(e.Context, new SvnOrigin[] { section.Origin }, section.Revision, null);

                    break;
            }
        }        
        
        static void PerformLog(IAnkhServiceProvider context, ICollection<SvnOrigin> targets, SvnRevision start, SvnRevision end)
        {
            IAnkhPackage package = context.GetService<IAnkhPackage>();

            package.ShowToolWindow(AnkhToolWindow.Log);

            LogToolWindowControl logToolControl = context.GetService<ISelectionContext>().ActiveFrameControl as LogToolWindowControl;
            if (logToolControl != null)
                logToolControl.StartLog(context, targets, start, end);
        }
    }
}
