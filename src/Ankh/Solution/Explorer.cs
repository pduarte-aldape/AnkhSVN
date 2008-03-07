// $Id$
using System;
using EnvDTE;
using Utils;
using Utils.Win32;

using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.IO;

using DteConstants = EnvDTE.Constants;
using System.Windows.Forms;
using System.Threading;
using Ankh.UI;


namespace Ankh.Solution
{

    /// <summary>
    /// Represents the Solution Explorer window in the VS.NET IDE
    /// </summary>
    public class Explorer : ISolutionExplorer
    {
        public Explorer( _DTE dte, IContext context )
        {
            this.dte = dte;
            this.context = context;
            this.nodes = new Hashtable();
            
            // get the uihierarchy root
            this.solutionNode = null;

            //this.SetUpTreeview();
        }

        /// <summary>
        ///  To be called when a solution is loaded.
        /// </summary>
        public void Load()
        {           
            this.Unload();
            this.context.ProjectFileWatcher.AddFile( this.DTE.Solution.FullName );
            this.SetUpTreeview();
            this.SyncAll();

            Trace.WriteLine( String.Format( "Cache hit rate: {0}%", 
                this.context.StatusCache.CacheHitSuccess ), "Ankh" );
        }

        /// <summary>
        ///  Resets the state of the object. To be called when a solution is unloaded.
        /// </summary>
        public void Unload()
        {
            Debug.WriteLine( "Unloading existing solution information", "Ankh" );
            this.nodes.Clear();

            // make sure to use the field, not the property
            // the property will always create a TreeView and will never return null
            if ( this.treeView != null )
            {
                this.treeView.ClearStatusImages();

                // if someone wants VSS images now, let them.
                this.treeView.SuppressStatusImageChange = false;

                if ( this.originalImageList != IntPtr.Zero )
                {
                    this.treeView.StatusImageList = originalImageList;
                    originalImageList = IntPtr.Zero;
                }
            }
            this.context.ProjectFileWatcher.Clear();
            this.solutionNode = null;
        }    

        /// <summary>
        /// Refreshes the parents of the selected items.
        /// </summary>
        public void RefreshSelectionParents()
        {
            this.ForcePoll();

            foreach( UIHierarchyItem item in (Array)this.UIHierarchy.SelectedItems )
            {
                SolutionExplorerTreeNode node = this.GetNode( item );
                if ( node != null )
                {
                    if ( node == this.solutionNode )
                    {
                        this.RefreshNode( node );
                    }
                    else
                    {
                        this.RefreshNode( node.SolutionExplorerParent );
                    }
                }
            }
        }

        

        /// <summary>
        /// Refreshes all subnodes of a specific project.
        /// </summary>
        /// <param name="project"></param>
        public void Refresh( Project project )
        {
            this.ForcePoll();

            SolutionExplorerTreeNode node = this.GetNodeForProject( project );
            if ( node != null )
            {
                this.RefreshNode( node );
            }
        }

        
       
        /// <summary>
        /// Refreshes the current selection.
        /// </summary>
        public void RefreshSelection()
        {
            this.ForcePoll();

            foreach( UIHierarchyItem item in (Array)this.UIHierarchy.SelectedItems )
            {
                SolutionExplorerTreeNode node = this.GetNode( item );
                if ( node != null )
                {
                    this.RefreshNode( node );
                }
            }

            CountResources();
        }

       

        public void RemoveProject( Project project )
        {
            //SolutionExplorerTreeNode node = this.GetNodeForProject( project );
            /*if ( node != null )
            {
                node.Remove();
            }*/
        }

        /// <summary>
        /// Since the ItemAdded event is fired before IVTDPE.OnAfterAddedFilesEx, we need to set up a 
        /// refresh after a certain interval.
        /// </summary>
        public void SetUpDelayedProjectRefresh(IRefreshableProject project)
        {
            this.SetUpRefresh( new TimerCallback( this.ProjectRefreshCallback ), project );
        }


        public void SetUpDelayedSolutionRefresh()
        {
            this.SetUpRefresh( new TimerCallback( this.SolutionRefreshCallback ), null );
        }

        private void SetUpRefresh( TimerCallback callback, object state )
        {
            lock ( this )
            {
                // Avoid multiple refreshes if more things are added simultaneously
                if ( !this.refreshPending )
                {
                    this.refreshPending = true;
                    this.timer = new System.Threading.Timer(
                       new TimerCallback( callback ), state, REFRESHDELAY,
                       Timeout.Infinite );
                }
            }
        }

        


        public void SyncAll()
        {
            // build the whole tree anew
            Debug.WriteLine( "Synchronizing with treeview", "Ankh" );

            this.nodes.Clear();
            
            // store the original image list (check that we're not storing our own statusImageList
            if( StatusImages.StatusImageList.Handle != this.TreeView.StatusImageList )
                this.originalImageList = this.TreeView.StatusImageList;
            
            // and assign the status image list to the tree
            this.TreeView.StatusImageList = StatusImages.StatusImageList.Handle;
            this.treeView.SuppressStatusImageChange = true;


            // make sure everything's up to date.
            this.ForcePoll();
 
            Debug.WriteLine( "Created solution node", "Ankh" );
        }

        private void RefreshNode( SolutionExplorerTreeNode treeNode )
        {
            try
            {
                this.treeView.LockWindowUpdate();
                treeNode.Refresh();
            }
            finally
            {
                this.treeView.UnlockWindowUpdate();
            }
        }

        /// <summary>
        /// Returns the SvnItem resources associated with the selected items
        /// in the solution explorer.
        /// </summary>
        /// <param name="getChildItems">Whether children of the items in 
        /// question should be included.</param>        /// 
        /// <returns>A list of SvnItem instances.</returns>
        public IList GetSelectionResources( bool getChildItems )
        {
            return this.GetSelectionResources( getChildItems, null );
        }

        /// <summary>         
        /// Visits all the selected nodes.         
        /// </summary>         
        /// <param name="visitor"></param>         
        public void VisitSelectedNodes( INodeVisitor visitor )         
        {
            this.ForcePoll();

            //foreach( SelectedItem item in items )         
            object o = this.UIHierarchy.SelectedItems;         
            foreach( UIHierarchyItem item in (Array)this.UIHierarchy.SelectedItems )         
            {         
                SolutionExplorerTreeNode node = this.GetNode( item );         
                if ( node != null )         
                    node.Accept( visitor );         
            }         
        }

        


        /// <summary>
        /// Returns the SvnItem resources associated with the selected items
        /// in the solution explorer.
        /// </summary>
        /// <param name="getChildItems">Whether children of the items in 
        /// question should be included.</param>
        /// <param name="filter">A callback used to filter the items
        /// that are added.</param>
        /// <returns>A list of SvnItem instances.</returns>
        public IList GetSelectionResources( bool getChildItems, 
            ResourceFilterCallback filter )
        {
            this.ForcePoll();

            ArrayList list = new ArrayList();

            object o = this.UIHierarchy.SelectedItems;         
            foreach( UIHierarchyItem item in (Array)this.UIHierarchy.SelectedItems )         
            {         
                SolutionExplorerTreeNode node = this.GetNode( item );         
                if ( node != null )         
                    node.GetResources( list, getChildItems, filter );         
            }

            return list;
        }

        /// <summary>
        /// Returns all  the SvnItem resources from root
        /// </summary>
        /// <param name="filter">A callback used to filter the items
        /// that are added.</param>
        /// <returns>A list of SvnItem instances.</returns>
        public IList GetAllResources( ResourceFilterCallback filter )
        {
            if ( !context.AnkhLoadedForSolution )
                return new SvnItem[]{};

            this.ForcePoll();

            ArrayList list = new ArrayList();

            SolutionExplorerTreeNode node = solutionNode;     
            if ( node != null )         
                node.GetResources( list, true, filter );         

            return list;
        }


        internal Win32TreeView TreeView
        {
            //[System.Diagnostics.DebuggerStepThrough]
            get
            {
                if ( this.treeView == null )
                {
                    SetUpTreeview();
                }

                return this.treeView; 
            }
        }

        internal SolutionNode SolutionNode
        {
            get { return solutionNode; }
        }


        public bool RenameInProgress
        {
            get
            {
                return this.TreeView.RenameInProgress;
            }
        }

        internal IContext Context
        {
            get{ return this.context; }
        }

        internal _DTE DTE
        {
            [System.Diagnostics.DebuggerStepThrough]
            get{ return this.dte; }
        }

        internal UIHierarchy UIHierarchy
        {
            get
            {
                if ( this.uiHierarchy == null )
                {
                    this.uiHierarchy = (UIHierarchy)this.dte.Windows.Item(
                        DteConstants.vsWindowKindSolutionExplorer ).Object; 
                }
                return this.uiHierarchy;
            }
        }

        /// <summary>
        /// Retrieves the window handle to the solution explorer treeview and uses it
        /// to replace it's imagelist with our own.
        /// </summary>
        internal void SetUpTreeview()
        {
            Debug.WriteLine( "Setting up treeview", "Ankh" );
            Window solutionExplorerWindow = this.dte.Windows.Item(
                EnvDTE.Constants.vsWindowKindSolutionExplorer);

            // Get the caption of the solution explorer            
            string slnExplorerCaption = solutionExplorerWindow.Caption;
            Debug.WriteLine( "Caption of solution explorer window is " + slnExplorerCaption, 
                "Ankh" );

            IntPtr vsnet = (IntPtr)this.dte.MainWindow.HWnd;

            // Try searching for it among VS' windows.
            IntPtr slnExplorer = this.SearchForSolutionExplorer( vsnet, slnExplorerCaption );

            // not there? Try looking for a floating palette. These are toplevel windows for 
            // some reason
            if ( slnExplorer == IntPtr.Zero )
            {
                Debug.WriteLine( "Solution explorer not a child of VS.NET window. " +
                    "Searching floating windows", "Ankh" );

                slnExplorer = this.SearchFloatingPalettes( slnExplorerCaption );
            }

            IntPtr uiHierarchy = Win32.FindWindowEx( slnExplorer, IntPtr.Zero, 
                UIHIERARCHY, null );
            IntPtr treeHwnd = Win32.FindWindowEx( uiHierarchy, IntPtr.Zero, TREEVIEW, 
                null );
 
            if ( treeHwnd == IntPtr.Zero )
                throw new ApplicationException( 
                    "Could not attach to solution explorer treeview. If the solution explorer " + 
                    "window is on a secondary monitor, " +
                    "try moving it to the primary during solution loading." );

            this.treeView = new Win32TreeView( treeHwnd );
            this.treeViewHoster = new Win32TreeViewHost( Win32.GetParent( treeHwnd ) );

            // we need to keep track of this 
            this.treeViewHoster.ItemExpanded += new ItemExpandedEventHandler( treeViewHoster_ItemExpanded );
        }

        void treeViewHoster_ItemExpanded( object sender, ItemExpandedEventArgs e )
        {
            try
            {
                if ( this.Context.AnkhLoadedForSolution && !SuspendItemExpandedScope.Suspended )
                {
                    MaybeRefreshNode( (IntPtr)e.HItem );
                }
            }
            catch ( Exception ex )
            {
                this.Context.ErrorHandler.Handle( ex );
            }
        }

        private void MaybeRefreshNode( IntPtr hItem )
        {
            /*using ( new SuspendItemExpandedScope() )
            {
                SearchHItemVisitor visitor = new SearchHItemVisitor( hItem );

                this.solutionNode.Accept( visitor );

                if ( visitor.FoundNode != null && visitor.FoundNode.Children.Count == 0 )
                {
                    this.RefreshNode( visitor.FoundNode );

                    // Refreshing has a tendency to leave the scroll bar so it shows the 
                    // last item in the project, we don't want that.
                    this.treeView.EnsureVisible( hItem );
                }
            }*/
        }

       

        /// <summary>
        /// Searches floating palettes for the solution explorer window.
        /// </summary>
        /// <param name="slnExplorerCaption"></param>
        /// <returns></returns>
        private IntPtr SearchFloatingPalettes( string slnExplorerCaption )
        {
            IntPtr floatingPalette = Win32.FindWindowEx( IntPtr.Zero, IntPtr.Zero, VBFLOATINGPALETTE, null );
            while ( floatingPalette != IntPtr.Zero )
            {
                IntPtr slnExplorer = this.SearchForSolutionExplorer( floatingPalette, slnExplorerCaption );
                if ( slnExplorer != IntPtr.Zero )
                {
                    return slnExplorer;
                }
                floatingPalette = Win32.FindWindowEx( IntPtr.Zero, floatingPalette, VBFLOATINGPALETTE, null );
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Searches recursively for the solution explorer window.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="caption"></param>
        /// <returns></returns>
        private IntPtr SearchForSolutionExplorer( IntPtr parent, string caption )
        {
            // is it directly under the parent?
            IntPtr solutionExplorer = Win32.FindWindowEx( parent, IntPtr.Zero, GENERICPANE, caption );
            if ( solutionExplorer != IntPtr.Zero )
                return solutionExplorer;

            IntPtr win = Win32.FindWindowEx( parent, IntPtr.Zero, null, null );
            while ( win != IntPtr.Zero )
            {
                solutionExplorer = SearchForSolutionExplorer( win, caption );
                if ( solutionExplorer != IntPtr.Zero )
                {
                    return solutionExplorer;
                }
                win = Win32.FindWindowEx( parent, win, null, null );
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Create overlay images for locked and read only files.
        /// </summary>
        private void CreateOverlayImages()
        {
            IntPtr imageList = this.TreeView.ImageList;

            Icon lockIcon = new Icon(
                this.GetType().Assembly.GetManifestResourceStream( LOCK_ICON ) );
            Icon readonlyIcon = new Icon(
                this.GetType().Assembly.GetManifestResourceStream( READONLY_ICON ) );
            Icon lockedAndReadonlyIcon = new Icon(
                this.GetType().Assembly.GetManifestResourceStream( LOCKEDREADONLY_ICON ) );

            int lockImageIndex = Win32.ImageList_AddIcon( imageList, lockIcon.Handle );
            int readonlyImageIndex = Win32.ImageList_AddIcon( imageList, readonlyIcon.Handle );
            int lockedAndReadonlyIndex = Win32.ImageList_AddIcon( imageList, lockedAndReadonlyIcon.Handle );

            // We don't abort here if the overlay image cannot be set
            if (  !Win32.ImageList_SetOverlayImage( imageList, lockImageIndex, LockOverlay ) )
                Trace.WriteLine( "Could not set overlay image for the lock icon" );

            if (  !Win32.ImageList_SetOverlayImage( imageList, readonlyImageIndex, ReadonlyOverlay ) )
                Trace.WriteLine( "Could not set overlay image for the readonly icon" );

            if ( !Win32.ImageList_SetOverlayImage( imageList, lockedAndReadonlyIndex, LockReadonlyOverlay ) )
                Trace.WriteLine( "Could not set overlay image for the lockreadonly icon" );

        }


        /// <summary>
        /// Adds a new resource to the tree.
        /// </summary>
        /// <param name="projectKey">The modeled ProjectItem or an unmodeled placeholder for it</param>
        /// <param name="parsedKey">A parsed item for unmodeled projects</param>
        /// <param name="node">Our own representation</param>
        internal void AddUIHierarchyItemResource( UIHierarchyItem item, SolutionExplorerTreeNode node )
        {
            this.nodes[ item ] = node;
        }

        internal void RemoveUIHierarchyResource( UIHierarchyItem uIHierarchyItem )
        {
            this.nodes.Remove( uIHierarchyItem );
        }

        internal void AddProjectFile( string projectFile )
        {
            if ( projectFile != null && projectFile.Trim() != String.Empty )
            {
                this.context.ProjectFileWatcher.AddFile( projectFile );
            }
        }



        [Conditional( "DEBUG" )]
        private void CountResources()
        {
            //this.CountUIHierarchy();
            Debug.WriteLine( "Number of nodes in nodes hash: " + this.nodes.Count );
        }

        private void CountUIHierarchy()
        {
            EnvDTE.UIHierarchy hierarchy = this.dte.Windows.Item( EnvDTE.Constants.vsWindowKindSolutionExplorer ).Object as EnvDTE.UIHierarchy;

            int count = CountUIHierarchyItems( hierarchy.UIHierarchyItems );

            Debug.WriteLine( "UIHiearchyItems.Count: " + count );
        }

        private int CountUIHierarchyItems( UIHierarchyItems items )
        {
            int count = items.Count;
            foreach ( UIHierarchyItem item in items )
            {
                count += CountUIHierarchyItems( item.UIHierarchyItems );
            }

            return count;
        }

        internal void SetSolution( SolutionExplorerTreeNode node )
        {
            // we assume theres only one of these
            this.solutionNode = (SolutionNode)node;
        }

        private SolutionExplorerTreeNode GetNodeForProject( Project project )
        {
            EnvDTE.UIHierarchy hierarchy = this.dte.Windows.Item( EnvDTE.Constants.vsWindowKindSolutionExplorer ).Object as EnvDTE.UIHierarchy;

            if ( hierarchy.UIHierarchyItems.Count < 1 )
            {
                return null;
            }

            string hierarchyPath = hierarchy.UIHierarchyItems.Item(1).Name + "\\" + this.BuildHierarchyPathForProject( project );

            try
            {
                UIHierarchyItem item = hierarchy.GetItem( hierarchyPath );
                return this.GetNode( item );
            }
            catch ( Exception )
            {
                
                SolutionExplorerTreeNode node = SearchForProject( project, hierarchy.UIHierarchyItems.Item(1).UIHierarchyItems );
                //if ( node == null )
                //{
                //    DumpHierarchy( hierarchy );  // DumpHierarchy expands all tree nodes, don't enable in non-debug builds
                //}
                return node;
            }
        }

        private SolutionExplorerTreeNode SearchForProject( Project project, UIHierarchyItems hierarchyItems )
        {
            try
            {
                foreach ( UIHierarchyItem item in hierarchyItems )
                {
                    SolutionExplorerTreeNode treeNode = null;
                    if ( item.Object == project )
                    {
                        treeNode = GetNode( item ); ;
                    }
                    else if ( item.Object is Project )
                    {
                        treeNode = SearchPossibleSolutionFolder( project, item.Object as Project, item );
                    }
                    else if ( item.Object is ProjectItem )
                    {
                        // Children of a solution folder are ProjectItem objects. Their .Object are the actual children
                        ProjectItem projectItem = item.Object as ProjectItem;
                        Project childProject = DteUtils.GetProjectItemObject(projectItem) as Project;
                        if ( childProject != null )
                        {
                            if ( childProject == project )
                            {
                                treeNode = GetNode( item );
                            }
                            else
                            {
                                treeNode = SearchPossibleSolutionFolder( project, childProject, item );
                            }
                            
                        }
                    }

                    if ( treeNode != null )
                    {
                        return treeNode;
                    }
                }
                return null;
            }
            catch ( Exception )
            {
                return null;
            }
        }

        private SolutionExplorerTreeNode SearchPossibleSolutionFolder( Project project, Project possibleSolutionFolder, UIHierarchyItem item )
        {
            // is this a solution folder?
            if ( possibleSolutionFolder != null && possibleSolutionFolder.Kind == DteUtils.SolutionFolderKind )
            {
                return SearchForProject( project, item.UIHierarchyItems );
            }
            else
            {
                return null;
            }
        }

        [Conditional("DEBUG")]
        private void DumpHierarchy( UIHierarchy hierarchy )
        {
            DumpHierarchyItems( hierarchy.UIHierarchyItems, 0 );
        }

        private void DumpHierarchyItems( UIHierarchyItems uIHierarchyItems, int indent )
        {
            string indentString = new String( ' ', indent * 4 );
            foreach ( UIHierarchyItem item in uIHierarchyItems )
            {
                Debug.WriteLine( indentString + item.Name );
                DumpHierarchyItems( item.UIHierarchyItems, indent + 1 );
            }
        }

        private string BuildHierarchyPathForProject( Project project )
        {
            Project current = GetParentProject( project );
            string path = project.Name;
            while ( current != null )
            {
                path = current.Name + "\\" + path;
                current = GetParentProject( current );
            }

            return path;
        }

        private Project GetParentProject( Project project )
        {
            try
            {
                return project.ParentProjectItem != null
                        ? project.ParentProjectItem.ContainingProject
                        : null;
            }
            catch ( Exception )
            {
                return null;
            }
        }

        private SolutionExplorerTreeNode GetNode(UIHierarchyItem item)
        {
            if ( !this.context.AnkhLoadedForSolution )
                return null;

            //if ( this.uiHierarchy.UIHierarchyItems.Count == 0)
            {
                return null;
            }

            if ( item == this.UIHierarchy.UIHierarchyItems.Item( 1 ) )
                return this.solutionNode;
            else
                return this.nodes[ item ] as SolutionExplorerTreeNode;
        }

        /// <summary>
        /// Forces a poll of all project files.
        /// </summary>
        private void ForcePoll()
        {
            this.context.ProjectFileWatcher.ForcePoll();
        }

        private void ProjectRefreshCallback( object state )
        {
            try
            {
                IRefreshableProject project = state as IRefreshableProject;
                if ( project == null )
                {
                    throw new ArgumentException( "state must be a valid Project object", "state" );
                }

                

                // do we need to get back to the main thread?
                if ( this.context.UIShell.SynchronizingObject.InvokeRequired )
                {
                    this.context.UIShell.SynchronizingObject.Invoke( new System.Threading.TimerCallback( this.ProjectRefreshCallback ),
                        new object[] { project } );
                    return;
                }
                              
                // must make sure the project is still valid, since it may have been unloaded since the delayed refresh
                // was set up.
                if ( !this.RenameInProgress && project.IsValid )
                {
                    this.Refresh( project.Project );
                }
            }
            catch ( Exception ex )
            {
                this.Context.ErrorHandler.Handle( ex );
            }

            lock ( this )
            {
                this.refreshPending = false;
            }
        }

        private void SolutionRefreshCallback( object state )
        {
            try
            {
                // do we need to get back to the main thread?
                if ( this.context.UIShell.SynchronizingObject.InvokeRequired )
                {
                    this.context.UIShell.SynchronizingObject.Invoke( new System.Threading.TimerCallback( this.SolutionRefreshCallback ),
                        new object[] { null } );
                    return;
                }

                if ( !this.RenameInProgress )
                {
                    this.SyncAll();
                }
            }
            catch ( Exception ex )
            {
                this.context.ErrorHandler.Handle( ex );
            }
            
            lock ( this )
            {
                this.refreshPending = false;
            }
        }


        /// <summary>
        /// Merges the status icons with the icons for locked and read only.
        /// </summary>
        /// <param name="foundation"></param>
        /// <param name="b1"></param>
        /// <param name="b2"></param>
        /// <returns></returns>
        private Bitmap MergeStatusIcons( Bitmap foundation, Bitmap b1, Bitmap b2 )
        {
            Bitmap result = new Bitmap( foundation.Width * 4, foundation.Height );
            Graphics target = Graphics.FromImage( result );
            for( int i = 0; i < 4; i++ )
            {
                target.CompositingMode = CompositingMode.SourceCopy;
                target.DrawImage( foundation, foundation.Width * i, 0 );

                if ( i == 1 || i == 3 )
                {
                    target.CompositingMode = CompositingMode.SourceOver;
                    for( int j = 0; j < foundation.Width / b1.Width; j++ )
                    {
                        target.DrawImage( b1, foundation.Width * i + b1.Width * j, 0 );                        
                    }
                }

                if ( i == 2 || i == 3 )
                {
                    target.CompositingMode = CompositingMode.SourceOver;
                    for( int j = 0; j < foundation.Width / b2.Width; j++ )
                    {
                        target.DrawImage( b2, foundation.Width * i + b2.Width * j, 0 );                        
                    }

                }
            }
            return result;
        }

        private class SearchHItemVisitor : INodeVisitor
        {
            public SolutionExplorerTreeNode FoundNode;

            public SearchHItemVisitor( IntPtr hItem )
            {
                this.hItem = hItem;
            }

            public void VisitProject( ProjectNode node )
            {
                this.Visit( node );
            }

            public void VisitProjectItem( ProjectItemNode node )
            {
                this.Visit( node );
            }

            public void VisitSolutionNode( SolutionNode node )
            {
                this.Visit( node );
            }

            public void VisitSolutionFolder( SolutionFolderNode node )
            {
                this.Visit( node );
            }

            private void Visit( SolutionExplorerTreeNode node )
            {
                if ( node.HItem == this.hItem )
                {
                    this.FoundNode = node;
                }
                else
                {
                    foreach ( SolutionExplorerTreeNode child in node.Children )
                    {
                        child.Accept( this );
                    }
                }
            }

            private IntPtr hItem;
        }

        private class SuspendItemExpandedScope : IDisposable
        {
            public static bool Suspended = false;

            public SuspendItemExpandedScope()
            {
                Suspended = true;
            }

            public void Dispose()
            {
                Suspended = false;
            }
        }


        internal const int LockOverlay = 15;
        internal const int ReadonlyOverlay = 14;
        internal const int LockReadonlyOverlay = 13;
        private _DTE dte;
        private UIHierarchy uiHierarchy;
        private const string VSNETWINDOW = "wndclass_desked_gsk";
        private const string GENERICPANE = "GenericPane";
        private const string VSAUTOHIDE = "VsAutoHide";
        private const string UIHIERARCHY = "VsUIHierarchyBaseWin";
        private const string TREEVIEW = "SysTreeView32";
        private const string VBFLOATINGPALETTE = "VBFloatingPalette";
        private Hashtable nodes;
        private SolutionNode solutionNode;

        private IContext context;
        private Win32TreeView treeView;
        private Win32TreeViewHost treeViewHoster;
        private IntPtr originalImageList = IntPtr.Zero;

        private bool refreshPending;
        private System.Threading.Timer timer;
        protected const int REFRESHDELAY = 800;

        private const string LOCK_ICON = "Ankh.lock.ico";
        private const string READONLY_ICON = "Ankh.readonly.ico";
        private const string LOCKEDREADONLY_ICON = "Ankh.lockedreadonly.ico";
    }
}
