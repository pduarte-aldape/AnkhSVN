// $Id$
using System;
using System.Collections;
using System.IO;
using NSvn.Core;
using Utils;
using EnvDTE;
using System.Threading;

using Timer = System.Threading.Timer;

namespace Ankh
{
    /// <summary>
    /// Arguments for the ProjectFileModifiedDelegate
    /// </summary>
    public class FileModifiedEventArgs
    {
        public FileModifiedEventArgs( string file )
        {
            this.filename = file;
        }
        public string Filename
        {
            get{ return this.filename; }
        }

        private string filename;
    }

    public delegate void FileModifiedDelegate( object sender, 
        FileModifiedEventArgs args );

    /// <summary>
    /// Watches files.
    /// </summary>
    public class FileWatcher
    {
        /// <summary>
        /// A project file is modified.
        /// </summary>
        public event FileModifiedDelegate FileModified;

        public FileWatcher( Client client )
        {
            client.Notification += new NotificationDelegate(this.OnNotification);
            this.projectWatchers = new ArrayList();

            // set up the polling
            this.timer = new Timer( new TimerCallback( this.DoPoll ), null, 
                0, PollingInterval );
        }

        public void StartWatchingForChanges()
        {
            this.dirty = false;
        }

        /// <summary>
        /// Whether any projects have been modified since the last time 
        /// the watcher was reset.
        /// </summary>
        public bool HasDirtyFiles
        {
            get{ return this.dirty; }
        }

        /// <summary>
        /// Add a file to be watched.
        /// </summary>
        /// <param name="path"></param>
        public void AddFile( string path )
        {
            lock( this.projectWatchers )
            {
                if ( File.Exists( path ) )
                {
                    Watcher w = new Watcher( path, this );
                    this.projectWatchers.Add( w );
                }
            }
        }

        /// <summary>
        /// Clears the watchee list and performs a reset.
        /// </summary>
        public void Clear()
        {
            lock( this.projectWatchers )
            {
                this.projectWatchers.Clear();
            }
            this.dirty = false;
        }

        /// <summary>
        /// Resets the object
        /// </summary>
        public void Reset()
        {
            this.dirty = false;
        }

        /// <summary>
        /// Callback for SVN notifications.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnNotification( object sender, NotificationEventArgs args )
        {
            if ( IsChanged( args.ContentState ) || args.Action == NotifyAction.Revert )
            {
                if ( this.IsWatchee( args.Path ) )
                    this.dirty = true;
            }
        }

        /// <summary>
        /// Whether a NotifyState indicates a change to an item.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        private static bool IsChanged( NotifyState state )
        {
            return  state == NotifyState.Changed || 
                state == NotifyState.Conflicted ||
                state == NotifyState.Merged;
        }

        /// <summary>
        /// Whether a given path is being watched.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private bool IsWatchee( string path )
        {
            lock( this.projectWatchers )
            {
                foreach( Watcher w in this.projectWatchers )
                {
                    if ( PathUtils.NormalizePath(w.FilePath) == PathUtils.NormalizePath(path) )
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Timer callback. Performs the polling of the watchers.
        /// </summary>
        /// <param name="state"></param>
        private void DoPoll( object state )
        {
            lock( this.projectWatchers )
            {
                foreach( Watcher w in this.projectWatchers )
                    w.Poll();
            }
        }

        #region class Watcher
        private class Watcher 
        {
            public Watcher( string path, FileWatcher parent )
            {
                this.parent = parent;
                this.path = path;    
                this.lastWriteTime = File.GetLastWriteTime( this.path );
            }

            /// <summary>
            /// The path of this watcher.
            /// </summary>
            public string FilePath
            {
                get{ return this.path; }
            }
            

            /// <summary>
            /// Checks the last access time of this watcher.
            /// </summary>
            public void Poll()
            {
                DateTime now = File.GetLastWriteTime( this.path );
                if ( now - this.lastWriteTime > Delta )
                {                    
                    if ( this.parent.FileModified != null )
                    {
                        FileModifiedEventArgs projArgs = new 
                            FileModifiedEventArgs(this.path);
                        this.parent.FileModified( this.parent, projArgs );
                    }

                    this.lastWriteTime = now;
                }
            }

            private FileWatcher parent;
            private DateTime lastWriteTime;
            private string path;
            private static readonly TimeSpan Delta = new TimeSpan(0,0,1);
            
        }
        #endregion

        private ArrayList projectWatchers;
        private bool dirty;
        private Timer timer;
        private const long PollingInterval = 1000;
    }
}
