using System;
using System.IO;
using System.Text;
using EnvDTE;

namespace Ankh
{
	/// <summary>
	/// A TextWriter backed by the VS.NET output window.
	/// </summary>
	public class OutputPaneWriter : TextWriter
	{
		public OutputPaneWriter( _DTE dte, string caption )
		{
            OutputWindow window = (OutputWindow)dte.Windows.Item( 
                EnvDTE.Constants.vsWindowKindOutput).Object;
            this.outputPane = window.OutputWindowPanes.Add( caption );			
		}

        public override Encoding Encoding
        {
            get{ return Encoding.Default; }
        }

        /// <summary>
        /// Activate the pane.
        /// </summary>
        public void Activate()
        {
            this.outputPane.Activate();
        }

        /// <summary>
        /// Clear the pane.
        /// </summary>
        public void Clear()
        {
            this.outputPane.Clear();
        }

        public override void Write( char c )
        {
            this.outputPane.OutputString( c.ToString() );
        }

        public override void Write( string s )
        {
            this.outputPane.OutputString( s );
        }

        private OutputWindowPane outputPane;
	}
}
