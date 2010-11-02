// Small Text / GUI IRC client in .NET
// Copyright © 2010 Michael Fried

// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

// To compile using the Microsoft C# compiler or mono use one of the following commands:
// csc /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /filealign:512 /out:IrcClient.exe /target:winexe IrcClient.cs
// gmcs -nowarn -reference:System.dll -reference:System.Drawing.dll -reference:System.Windows.Forms.dll -out:IrcClient.exe -target:winexe IrcClient.cs
// csc.exe is typically found in your path under %windir%\Microsoft.NET\Framework\(version)\csc.exe

// To customize the behavior of the Client/Server communication, you need to edit
// ProcessServerResponse or ProcessClientInput in the Networking.IrcClient class.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Collections.Generic;

[assembly: AssemblyTitle( "IRC Client" )]
[assembly: AssemblyDescription( "A small text-based IRC client" )]
[assembly: AssemblyConfiguration( "" )]
[assembly: AssemblyCompany( "" )]
[assembly: AssemblyProduct( "IrcClient" )]
[assembly: AssemblyCopyright( "Copyright © Michael Fried 2010" )]
[assembly: AssemblyTrademark( "" )]
[assembly: AssemblyCulture( "" )]
[assembly: ComVisible( false )]
[assembly: Guid( "55bf7001-bd1b-446c-95d9-aa3395ebaf2f" )]
[assembly: AssemblyVersion( "1.0.0.0" )]
[assembly: AssemblyFileVersion( "1.0.0.0" )]

namespace IrcClient
{
   public class Setting
   {
      public string Name;
      public Type SettingType;
      public object Value;
      public string ShortDescription;
      public string Description;
      public Setting( string name, object value, string shortDescription, string description )
      {
         Name = name;
         Value = value;
         SettingType = value.GetType();
         ShortDescription = shortDescription;
         Description = description;
      }
      public bool Parse( string value )
      {
         if( SettingType == typeof( string ) )
            Value = (string) value;
         else if( SettingType == typeof( int ) )
         {
            int intValue;
            if( !int.TryParse( value, out intValue ) )
               return false;
            Value = intValue;
         }
         else if( SettingType == typeof( bool ) )
         {
            bool boolValue;
            if( !bool.TryParse( value, out boolValue ) )
               return false;
            Value = boolValue;
         }
         else
            return false;
         return true;
      }
      public override string ToString()
      {
         return string.Format( "{0}={1}", Name, Value );
      }
   }

   public static class Settings
   {
      private static List< Setting > s_settings = new List< Setting >()
      {
         new Setting( "UseGUI", true, "true / false", "launch graphical ui vs text ui" ),
         new Setting( "Server", "irc.gimp.org", "server address", "the IRC server" ),
         new Setting( "Port", 6666, "Port number", "TCP port of IRC server" ),
         new Setting( "Nick", "guestuser", "nickname", "your nick name" ),
         new Setting( "User", string.Format( "{0}@{1}", Environment.UserName, Environment.MachineName ), "user@host", "user name / machine name" ),
         new Setting( "Password", "nopass", "secretpassword", "insecure IRC server password" ),
         new Setting( "FullName", "Anonymous Coward", "full name", "your name IRL" ),
      };

      public static Setting GetSetting( string name )
      {
         foreach( Setting setting in s_settings )
            if( setting.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) )
               return setting;
         return null;
      }

      static string[] optionPrefixes = new string[] { "--", "-", "/" };
      static char[] nameValueSeparators = new char[] { '=', ':', '-' };

      public static bool MakeSettings( string[] args )
      {
         string settingsFilePath = string.Format( "{0}{1}irc.settings", Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData ), Path.DirectorySeparatorChar );
         if( File.Exists( settingsFilePath ) )
            foreach( string line in File.ReadAllLines( settingsFilePath ) )
               ParseSetting( line );

         for( int iArg = 0; iArg < args.Length; iArg++ )
         {
            string arg = args[ iArg ];

            // Remove argument prefix
            if( arg.StartsWith( "--" ) )
               arg = arg.Substring( 2 );
            else if( arg.StartsWith( "-" ) || arg.StartsWith( "/" ) )
               arg = arg.Substring( 1 );

            // Either a failed parse or an argument for help
            if( !ParseSetting( arg ) )
            {
               using( Win32ConsoleWorkaround win32MightNeedConsole = new Win32ConsoleWorkaround() )
               {
                  Console.WriteLine( "Small C# IRC Client" );
                  Console.WriteLine( "Copyright (C) 2010 Michael Fried" );
                  if( arg != "?" && !arg.StartsWith( "help", StringComparison.OrdinalIgnoreCase ) )
                     Console.WriteLine( "{1}Syntax Error: invalid argument: {0}{1}", arg, Environment.NewLine );
                  Console.WriteLine( "Usage: irc.exe {options}" );
                  Console.WriteLine( "Options are not case sensitive" );
                  Console.WriteLine( "Options may be prefixed with /, -, or --" );
                  Console.WriteLine( "Options may be separated from values by :, =, or -" );
                  Console.WriteLine();
                  Console.WriteLine( "Where options can be any of:" );
                  foreach( Setting setting in s_settings )
                     Console.WriteLine( "\"{0}:{{{1}}}\" {2} (currently {3}).", setting.Name, setting.ShortDescription, setting.Description, setting.Value );
                  Console.WriteLine();
                  Console.WriteLine( "Settings are saved in {0}", settingsFilePath );
                  if( win32MightNeedConsole.MadeConsole )
                  {
                     Console.WriteLine( "Press any key to exit." );
                     Console.ReadKey( false );
                  }
                  return false;
               }
            }
         }

         List<string> settingLines = new List<string>();
         foreach( Setting setting in s_settings )
            settingLines.Add( setting.ToString() );
         File.WriteAllLines( settingsFilePath, settingLines.ToArray() );
         return true;
      }

      public static bool ParseSetting( string arg )
      {
         int separatorLoc = arg.IndexOfAny( nameValueSeparators );
         string name = separatorLoc < 0 ? arg : arg.Substring( 0, separatorLoc );
         Setting setting = GetSetting( name );
         if( setting == null )
            return false;
         string value = separatorLoc < 0 ? "" : arg.Substring( separatorLoc + 1 );
         return setting.Parse( value );
      }
   }

   public static class IrcClient
   {
      [STAThread]
      public static void Main( string[] args )
      {
         if( !Settings.MakeSettings( args ) )
            return;

         if( (bool) Settings.GetSetting( "UseGUI" ).Value )
         {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault( false );
            Application.Run( new ChatWindow() );
         }
         else
         {
            using( Win32ConsoleWorkaround win32MightNeedConsole = new Win32ConsoleWorkaround() )
            {
               Networking.IrcClient client = new Networking.IrcClient( WriteIrcLine,
                  (string) Settings.GetSetting( "Server" ).Value, (int) Settings.GetSetting( "Port" ).Value );
               client.Run( (string) Settings.GetSetting( "Password" ).Value,
                           (string) Settings.GetSetting( "Nick" ).Value,
                           (string) Settings.GetSetting( "User" ).Value,
                           (string) Settings.GetSetting( "FullName" ).Value );
               while( client.Running )
                  client.ProcessClientInput( Console.ReadLine() );
               if( win32MightNeedConsole.MadeConsole )
               {
                  Console.WriteLine( "Press any key to exit." );
                  Console.ReadKey( false );
               }
            }
         }
      }

      public static void WriteIrcLine( string toWhere, string line )
      {
         Console.WriteLine( line );
      }
   }

   // Workaround for having the default Win32 behavior be hiding
   // the console if not launched from a console.
   public class Win32ConsoleWorkaround : IDisposable
   {
      #region Win32 Interop
      [DllImport( "kernel32.dll" )]
      private static extern IntPtr GetConsoleWindow();
      [DllImport( "kernel32.dll" )]
      private static extern bool AllocConsole();
      [DllImport( "kernel32.dll" )]
      private static extern bool FreeConsole();
      #endregion
      public static bool OnWindows()
      {
         return Environment.OSVersion.Platform == PlatformID.Win32NT ||
                Environment.OSVersion.Platform == PlatformID.Win32Windows;
      }
      public static bool NoWin32Console { get { return IntPtr.Zero == GetConsoleWindow(); } }
      private bool m_fMadeWin32Console = false;
      public bool MadeConsole { get { return m_fMadeWin32Console; } }
      public Win32ConsoleWorkaround()
      {
         if( OnWindows() && NoWin32Console )
            m_fMadeWin32Console = AllocConsole();
      }
      void IDisposable.Dispose()
      {
         if( m_fMadeWin32Console )
            m_fMadeWin32Console = !FreeConsole();
      }
   }

   // Not designable Windows Form.
   [System.ComponentModel.DesignerCategory( "" )]
   public class ChatWindow : Form
   {
      public string m_server = (string) Settings.GetSetting( "Server" ).Value;
      public int m_port = (int) Settings.GetSetting( "Port" ).Value;
      public string m_nick = (string) Settings.GetSetting( "Nick" ).Value;
      public string m_user = (string) Settings.GetSetting( "User" ).Value;
      public string m_name = (string) Settings.GetSetting( "FullName" ).Value;
      public string m_pass = (string) Settings.GetSetting( "Password" ).Value;

      private Networking.IrcClient m_client;

      private TextBox m_chatArea;
      private TextBox m_textEntry;
      private ToolStrip m_statusBar;
      private ToolStripStatusLabel m_statusLabel;
      private MenuStrip m_menu;
      private ToolStripMenuItem m_clientMenu;
      private ToolStripMenuItem m_connectMenuItem;
      private ToolStripMenuItem m_exitMenuItem;

      public ChatWindow()
      {
         // Init UI
         m_chatArea = new TextBox()
         {
            Dock = DockStyle.Fill, TabIndex = 1, BackColor = System.Drawing.SystemColors.Window,
            Cursor = Cursors.Hand, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
         };
         m_textEntry = new TextBox() { Dock = DockStyle.Bottom, TabIndex = 0 };
         m_statusLabel = new ToolStripStatusLabel( "" );
         m_statusBar = new ToolStrip( m_statusLabel ) { Dock = DockStyle.Bottom };
         m_menu = new MenuStrip() { TabIndex = 2 };
         m_clientMenu = new ToolStripMenuItem( "&Client" );
         m_connectMenuItem = new ToolStripMenuItem( "&Connect" );
         m_exitMenuItem = new ToolStripMenuItem( "E&xit" );

         // Callbacks
         m_chatArea.PreviewKeyDown += new PreviewKeyDownEventHandler( chatArea_PreviewKeyDown );
         m_textEntry.KeyDown += new KeyEventHandler( textEntry_KeyDown );
         m_connectMenuItem.Click += new System.EventHandler( OnClickConnect );
         m_exitMenuItem.Click += new System.EventHandler( OnClickExit );

         // Layout
         m_clientMenu.DropDownItems.AddRange( new ToolStripItem[] { m_connectMenuItem, m_exitMenuItem } );
         m_menu.Items.Add( m_clientMenu );
         ClientSize = new System.Drawing.Size( 600, 400 );
         Controls.AddRange( new Control[] { m_chatArea, m_textEntry, m_statusBar, m_menu } );
         MainMenuStrip = m_menu;
         Text = "IRC Chat Client";
         m_client = new Networking.IrcClient( WriteIrcLine, m_server, m_port );
      }

      private void chatArea_PreviewKeyDown( object sender, PreviewKeyDownEventArgs e )
      {
         m_textEntry.Focus();
      }

      private void textEntry_KeyDown( object sender, KeyEventArgs e )
      {
         if( e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return )
         {
            e.SuppressKeyPress = true;
            RunCommand( m_textEntry.Text.Trim() );
            m_textEntry.Text = "";
         }
      }

      private void RunCommand( string command )
      {
         if( m_client.Running )
            m_client.ProcessClientInput( command );
      }

      protected override void OnClosing( System.ComponentModel.CancelEventArgs e )
      {
         Stop();
         base.OnClosing( e );
      }

      private void WriteIrcLine( string toWhere, string line )
      {
         if( toWhere.Equals( "Status" ) )
            StatusLine( line );
         else
            WriteLine( string.Format( "{0} {1}", toWhere, line ) );
      }

      #region InvokeRequired feedback
      private delegate void SingleLineDelegate( string line );
      private void StatusLine(string line)
      {
         if( line == null || this == null || IsDisposed )
            return;
         if( m_statusBar.InvokeRequired )
         {
            m_statusBar.Invoke( new SingleLineDelegate( StatusLine ), line );
            return;
         }
         m_statusLabel.Text = line;
      }

      private void WriteLine( string format, params object[] args ) { WriteLine( string.Format( format, args ) ); }
      private void WriteLine( string line )
      {
         // Because clients might in theory outlive this window and due to non-determinism
         // in shutdown of threads, let's just be on the safe side here.
         if( line == null || this == null || IsDisposed )
            return;
         if( m_chatArea.InvokeRequired )
         {
            m_chatArea.Invoke( new SingleLineDelegate( WriteLine ), line );
            return;
         }
         m_chatArea.AppendText( string.Format( "{0}{1}", line, System.Environment.NewLine ) );
      }

      private delegate void SetConnectEnabledDelegate( bool fEnabled );
      private void SetConnectEnabled( bool fEnabled )
      {
         if( IsDisposed )
            throw new ObjectDisposedException( Name );

         if( InvokeRequired )
         {
            Invoke( new SetConnectEnabledDelegate( SetConnectEnabled ), fEnabled );
            return;
         }

         m_connectMenuItem.Enabled = fEnabled;
      }
      #endregion

      private void OnClickConnect( object sender, EventArgs e )
      {
         if( m_client == null )
            m_client = new Networking.IrcClient( WriteIrcLine, m_server, m_port );
         else if( m_client.Running )
            return;
         m_client.Run( m_pass, m_nick, m_user, m_name );
      }

      private void OnClickExit( object sender, EventArgs e )
      {
         Close();
      }

      private void Stop()
      {
         if( m_client != null )
         {
            m_client.Stop();
            m_client = null;
         }
         SetConnectEnabled( true );
      }
   }
}

namespace Networking
{
   public class IrcClient
   {
      public delegate void OutputLineWriter( string toWhere, string line );
      private OutputLineWriter m_outputLineWriter;
      private Client m_client;
      private string m_pass = "";
      private string m_nick = "";
      private string m_user = "";
      private string m_fullname = "";
      private List<string> m_channels = new List<string>();
      private string m_channel = "";

      public IrcClient( OutputLineWriter outputLineWriter, string hostName, int port )
      {
         m_outputLineWriter = outputLineWriter;
         OutLine( "Status", "Creating client for {0} port {1}", hostName, port );
         m_client = new Client( LookupAddress( hostName ), port );
         m_client.Connect += new EventHandler( OnConnect );
         m_client.Error += new EventHandler<ErrorEventArgs>( OnError );
         m_client.ReceiveLine += new EventHandler<ReceiveLineArgs>( ProcessServerResponse );
         m_client.Disconnect += new EventHandler<DisconnectEventArgs>( OnDisconnect );
      }

      private volatile bool m_fRunning;
      public bool Running { get { return m_fRunning; } }

      public void Run( string pass, string nick, string user, string fullname )
      {
         m_pass = pass;
         m_nick = nick;
         m_user = user;
         m_fullname = fullname;

         m_client.Start();
         m_fRunning = true;
      }

      // See RFC 1459 Section 2.3.1
      private static Regex s_processResponse = new Regex(
@"^([:](?<prefix>(?<servername>[^ !@]+)|(?<nick>[a-zA-Z][^ !@]+)([!](?<user>[^ @]+))?([@](?<host>[^ ]+))?)[ ]+)?
   (?<command>[a-zA-Z]+|[0-9][0-9][0-9])(?<params>([ ]+(?<param>[^:][^ ]*))*([ ]+[:](?<param>.*))?)$",
         RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace );

      // This struct is useful to look at in the debugger to understand a message.
      [DebuggerDisplay( "{m_line}" )]
      public struct IRCMessage
      {
         public bool     Succeeded;
         public bool     UserInitiated;
         public string   Prefix;
         public string   Server;
         public string   Nick;
         public string   User;
         public string   Host;
         public string   Command;
         public int      Code;
         public string   AllParameters;
         public string[] Parameters;
         public string   Trailing;
         private string m_line;

         public IRCMessage( string line )
         {
            m_line = line;
            Match processed = s_processResponse.Match( line );
            Succeeded = processed.Success;
            Prefix = processed.Groups[ "prefix" ].Value;
            Server = processed.Groups[ "servername" ].Value;
            Nick = processed.Groups[ "nick" ].Value;
            UserInitiated = processed.Groups[ "nick" ].Success;
            User = processed.Groups[ "user" ].Value;
            Host = processed.Groups[ "host" ].Value;
            Command = processed.Groups[ "command" ].Value;
            Code = -1;
            AllParameters = processed.Groups[ "params" ].Value;
            if( Command.Length == 3 )
               int.TryParse( Command, out Code );
            CaptureCollection parameters = processed.Groups[ "param" ].Captures;
            Parameters = parameters.Count > 0 ? new string[ parameters.Count ] : null;
            for( int iParam = 0; iParam < parameters.Count; iParam++ )
               Parameters[ iParam ] = parameters[ iParam ].Value;
            Trailing = parameters.Count > 0 ? Parameters[ parameters.Count - 1 ] : "";
         }

         public override string ToString() { return m_line; }
      }

#if DEBUG
      // Last 1024-2048 messages from Client and Server (mixed)
      List< IRCMessage > m_messages = new List<IRCMessage>();
#endif

      [Conditional( "DEBUG" )]
      private void DebugAddMessage( IRCMessage message )
      {
#if DEBUG
         if( m_messages.Count > 2048 )
            m_messages.RemoveRange( 0, 1024 );
         m_messages.Add( message );
#endif
      }

      private void ProcessServerResponse( object sender, ReceiveLineArgs e )
      {
         IRCMessage ircMessage = new IRCMessage( e.Line );
         DebugAddMessage( ircMessage );
         if( !ircMessage.Succeeded )
         {
            OutLine( "Error", "Unable to parse message: {0}", e.Line );
            return;
         }

         string initiator = ircMessage.UserInitiated ? ircMessage.Nick : ircMessage.Server;
         if( ircMessage.Code >= 0 )
         {
            int code = ircMessage.Code;
            // Note: a good resource for these codes is here: http://www.mirc.net/raws/
            // <100: Information about the connection / the server
            // 200-299: Server info, Status, MOTD
            // 300-399: Info you request from the server, users, channels, etc
            // 400-499: Errors
            // 500-599: Mode errors, Mode settings
            // 600-699: 
            if( code == 1 )
               OutLine( "Status", "Connected to server." );
            else if( code >= 200 && code < 400 )
               OutLine( initiator, "{0}: {1}", ircMessage.Command, ircMessage.AllParameters );
            else if( code >= 400 && code < 500 )
               OutLine( initiator, "Error code {0}: {1}", code, ircMessage.AllParameters );
            return;
         }

         string command = ircMessage.Command.ToUpperInvariant();
         switch( command )
         {
            case "AUTH":
            case "NOTICE":
            case "ERROR":
               break;
            case "MODE":
               OutLine( "", "{0} sets MODE {2} on {1}.", initiator, ircMessage.Parameters[ 0 ], ircMessage.Parameters[ 1 ] );
               break;
            case "NICK":
               OutLine( "", "{0} is now known as {1}.", ircMessage.Nick, ircMessage.Trailing );
               break;
            case "JOIN":
            case "PART":
            {
               bool fJoin = command.Equals( "JOIN" );
               string verb = fJoin ? "entering" : "leaving";
               string channel = ircMessage.Trailing;
               if( ircMessage.Nick.Equals( m_nick ) )
               {
                  OutLine( "Status", "You are now {0} channel {1}.", verb, channel );
                  if( fJoin )
                  {
                     m_channels.Add( channel );
                     m_channel = channel;
                  }
                  else
                  {
                     m_channels.Remove( channel );
                     if( m_channels.Count < 1 )
                        m_channel = "";
                     else
                        m_channel = m_channels[ m_channels.Count - 1 ];
                  }
               }
               else
                  OutLine( channel, "{0} is now {1} channel {2}.", ircMessage.Nick, verb, channel );
               break;
            }
            case "PING":
               SendLine( e.Line.Replace( "PING", "PONG" ) );
               break;
            case "PRIVMSG":
               string message = ircMessage.Trailing;
               if( message.StartsWith( "\u0001ACTION" ) && message.EndsWith( "\u0001" ) )
                  message = string.Format( "* {0}", message.Substring( 7, message.Length - 8 ) );
               if( ircMessage.Parameters.Length > 0 )
                  OutLine( ircMessage.Parameters[ 0 ], "{0} <{1}> {2}", DateTimeString, ircMessage.Nick, message );
               break;
            case "QUIT":
               break;
            default:
               OutLine( command, "Unprocessed response: {0}", e.Line );
               break;
         }
      }

      // Time stamp format for output
      private static string DateTimeString { get { return DateTime.Now.ToString( "[hh:mm]" ); } }

      private void ChannelMessage( string format, string who, string what )
      {
         OutLine( m_channel, format, DateTimeString, who, what );
      }

      public void ProcessClientInput( string command )
      {
         command = command.Trim();
         if( command.Length < 1 )
            return;

         string remainder;
         if(      SendOnCommand( command, "/join #", "JOIN #{0}", out remainder ) )
            OutLine( "Status", "Attempting to join channel #{0}", remainder );
         else if( SendOnCommand( command, "/join ", "JOIN #{0}", out remainder ) )
            OutLine( "Status", "Attempting to join channel #{0}", remainder );
         else if( SendOnCommand( command, "/part #", "PART #{0}", out remainder ) )
            OutLine( "Status", "Attempting to depart from channel #{0}", remainder );
         else if( SendOnCommand( command, "/part ", "PART #{0}", out remainder ) )
            OutLine( "Status", "Attempting to depart from channel #{0}", remainder );
         else if( SendOnCommand( command, "/nick ", "NICK {0}", out remainder ) )
            m_nick = remainder;
         else if( SendOnCommand( command, "/me ", "PRIVMSG {0} :\u0001ACTION {1}\u0001", out remainder, m_channel ) )
            ChannelMessage( "{0} {1} * {2}", m_nick, remainder );
         else if( SendOnCommand( command, "/quit ", "QUIT :{0}", out remainder ) ||
                  SendOnCommand( command, "/quit", "QUIT", out remainder ) )
         {
            m_fRunning = false;
            OutLine( "Status", "Closing the connection to server." );
         }
         // Other / based commands can go here. The generic send IRC command and then the
         // enter text to channel commands go below.
         else if( SendOnCommand( command, "/", "{0}", out remainder ) )
            ChannelMessage( "{0} Sending server command: \"{1}\"", remainder, "" );
         else if( SendOnCommand( command, "", "PRIVMSG {0} :{1}", out remainder, m_channel ) )
            ChannelMessage( "{0} [{1}] {2}", m_nick, remainder );
         // The else condition here is that the user pressed enter or just inserted white space.
         // We ignore this condition.
      }

      private bool SendOnCommand( string commandLine, string pattern, string replace, out string remainder, params object[] args )
      {
         remainder = "";
         if( commandLine.Length < 1 || pattern.Length > 0 && !commandLine.StartsWith( pattern, StringComparison.InvariantCultureIgnoreCase ) )
            return false;
         remainder = commandLine.Substring( pattern.Length ).Trim();
         System.Collections.ArrayList newArgs = new System.Collections.ArrayList( args );
         newArgs.Add( remainder );
         SendLine( string.Format( replace, newArgs.ToArray() ) );
         return true;
      }

      private void SendLine( string line )
      {
         DebugAddMessage( new IRCMessage( line ) );
         m_client.Send( string.Format( "{0}\r\n", line ) );
      }

      public void OutLine( string toWhere, string message, params object[] args )
      {
         string line = string.Format( message, args );
         if( m_outputLineWriter != null )
            m_outputLineWriter( toWhere, line );
      }

      // Lookup the IP Address of a given DNS entry. If multiple IP addresses
      // exist, pick one at random.
      public static IPAddress LookupAddress( string hostName )
      {
         IPAddress address;
         IPAddress[] addresses = Dns.GetHostEntry( hostName ).AddressList;
         if( addresses.Length < 1 )
            address = null;
         if( addresses.Length > 1 )
            address = addresses[ new Random().Next( addresses.Length ) ];
         else
            address = addresses[ 0 ];
         return address;
      }

      void OnConnect( object sender, EventArgs e )
      {
         OutLine( "Status", "Connected to {0} {1}", ( sender as Client ).Address, ( sender as Client ).Port );
         OutLine( "Status", "Attempting to login with nick {0} ({1})", m_nick, m_fullname );
         SendLine( string.Format( "PASS {0}", m_pass ) );
         SendLine( string.Format( "NICK {0}", m_nick ) );
         SendLine( string.Format( "USER {0} {1} {2} :{3}", m_nick, 4, m_user, m_fullname ) );
      }

      void OnDisconnect( object sender, DisconnectEventArgs e )
      {
         OutLine( "Status", "Disconnected." );
         m_fRunning = false;
      }

      void OnError( object sender, ErrorEventArgs e )
      {
         m_fRunning = false;
         SocketException exc = ( e.GetException() as SocketException );
         OutLine( "Status", "Error: {0} ({1})", Enum.GetName( typeof( SocketError ), exc.SocketErrorCode ), exc.Message );
      }

      public void Stop() { Stop( "Quitting." ); }
      public void Stop( string reason )
      {
         m_client.Send( string.Format( "QUIT :{0}", reason ) );
         m_client.Stop();
         m_fRunning = false;
      }
   }

   public abstract class DisposablePattern : IDisposable
   {
      protected abstract void FreeManaged();
      protected abstract void FreeUnmanaged();

      private bool disposed;

      protected void CheckDisposed() { if( disposed ) throw new ObjectDisposedException( GetType().Name ); }

      public void Dispose()
      {
         Dispose( true );
         GC.SuppressFinalize( this );
      }

      protected virtual void Dispose( bool disposing )
      {
         if( !disposed )
         {
            if( disposing )
            {
               FreeManaged();
            }
            FreeUnmanaged();
            disposed = true;
         }
      }

      ~DisposablePattern()
      {
         Dispose( false );
      }
   }

   public class DisconnectEventArgs : EventArgs
   {
      private Client m_client;
      internal DisconnectEventArgs( Client client ) { m_client = client; }
      public Client Client { get { return m_client; } }
   }

   public class ReceiveLineArgs : EventArgs
   {
      private string m_line;
      internal ReceiveLineArgs( string line ) { m_line = line; }
      public string Line { get { return m_line; } }
      public override string ToString() { return m_line; }
   }

   public class ReceiveEventArgs : EventArgs
   {
      private byte[] m_buffer;
      private int m_start;
      private int m_length;

      public byte[] Buffer { get { return m_buffer; } }
      public int Start { get { return m_start; } }
      public int Length { get { return m_length; } }

      internal ReceiveEventArgs( byte[] buffer, int start, int length )
      {
         m_buffer = buffer;
         m_start = start;
         m_length = length;
      }
   }

   [DebuggerDisplay( "Client for {m_address} {m_port}" )]
   public class Client : DisposablePattern
   {
      private IPAddress m_address;
      public IPAddress Address { get { return m_address; } }

      private int m_port;
      public int Port { get { return m_port; } }

      public bool Connected { get { CheckDisposed(); return m_socket.Connected; } }

      public event EventHandler Connect;
      private void OnConnect( object sender, EventArgs e ) { }

      public event EventHandler<ReceiveEventArgs> Receive;
      private void OnReceive( object sender, ReceiveEventArgs e ) { }

      public EventHandler< ReceiveLineArgs > ReceiveLine;
      private void OnReceiveLine( object sender, ReceiveLineArgs e ) { }

      public EventHandler< ErrorEventArgs > Error;
      private void OnError( object sender, ErrorEventArgs e ) { }

      public EventHandler< DisconnectEventArgs > Disconnect;
      private void OnDisconnect( object sender, DisconnectEventArgs e ) { }

      private StringBuilder m_stringBuilder;
      private Socket m_socket;
      private byte[] m_buffer;
      private bool m_fServerSocket;
      private bool m_fStarted;

      private void InitEvents()
      {
         Connect = new EventHandler( OnConnect );
         Receive = new EventHandler<ReceiveEventArgs>( OnReceive );
         ReceiveLine = new EventHandler<ReceiveLineArgs>( OnReceiveLine );
         Error = new EventHandler<ErrorEventArgs>( OnError );
         Disconnect = new EventHandler<DisconnectEventArgs>( OnDisconnect );
      }

      public Client( IPAddress address, int port )
      {
         m_address = address;
         m_port = port;
         InitEvents();
         m_stringBuilder = new StringBuilder();
         m_socket = new Socket( address.AddressFamily, SocketType.Stream, ProtocolType.Tcp );
         m_buffer = new byte[ m_socket.ReceiveBufferSize ];
      }

      internal Client( Socket socket )
      {
         IPEndPoint remote = socket.RemoteEndPoint as IPEndPoint;
         m_address = remote.Address;
         m_port = remote.Port;
         InitEvents();
         m_fServerSocket = true;
         m_socket = socket;
         m_buffer = new byte[ m_socket.ReceiveBufferSize ];
      }

      public void Start()
      {
         CheckDisposed();
         try
         {
            m_fStarted = true;
            if( m_fServerSocket )
               m_socket.BeginReceive( m_buffer, 0, m_buffer.Length, 0, ReadCallback, null );
            else
               m_socket.BeginConnect( new IPEndPoint( m_address, m_port ), ConnectCallback, null );
         }
         catch( SocketException exception )
         {
            Error.Invoke( this, new ErrorEventArgs( exception ) );
         }
      }

      public void Send( string data )
      {
         CheckDisposed();
         try
         {
            byte[] byteData = Encoding.ASCII.GetBytes( data );
            m_socket.BeginSend( byteData, 0, byteData.Length, 0, SendCallback, null );
         }
         catch( SocketException exception )
         {
            Error.Invoke( this, new ErrorEventArgs( exception ) );
         }
      }

      public void Stop()
      {
         CheckDisposed();
         if( !m_fStarted )
            return;

         try
         {
            if( m_socket.Connected )
               m_socket.Shutdown( SocketShutdown.Both );

            if( m_socket.Connected )
               m_socket.Disconnect( false );
            m_socket.Close( 1 );
            m_fStarted = false;
            Disconnect.Invoke( this, new DisconnectEventArgs( this ) );
         }
         catch( SocketException exception )
         {
            Error.Invoke( this, new ErrorEventArgs( exception ) );
         }
      }

      private void ConnectCallback( IAsyncResult asyncResult )
      {
         CheckDisposed();
         try
         {
            m_socket.EndConnect( asyncResult );
            Connect.Invoke( this, EventArgs.Empty );
            m_socket.BeginReceive( m_buffer, 0, m_buffer.Length, 0, ReadCallback, null );
         }
         catch( SocketException e )
         {
            Error.Invoke( this, new ErrorEventArgs( e ) );
         }
      }

      int IndexOfNewLine( string content, ref int startPosition, ref int nextLineStart )
      {
         if( startPosition > content.Length )
         {
            nextLineStart = 0;
            return -1;
         }

         int index = content.IndexOf( Environment.NewLine, startPosition );
         startPosition = nextLineStart;
         nextLineStart = index + Environment.NewLine.Length;
         return index;
      }

      private void ReadCallback( IAsyncResult asyncResult )
      {
         if( !m_socket.Connected )
            return;
         CheckDisposed();
         try
         {
            int bytesRead = m_socket.EndReceive( asyncResult );
            if( bytesRead > 0 )
            {
               Receive.Invoke( this, new ReceiveEventArgs( m_buffer, 0, bytesRead ) );
               m_stringBuilder.Append( Encoding.ASCII.GetString( m_buffer, 0, bytesRead ) );

               string content = m_stringBuilder.ToString();
               int index = content.IndexOf( Environment.NewLine );
               int lastIndex = 0;
               while( index > -1 )
               {
                  string line = m_stringBuilder.ToString( lastIndex, index - lastIndex );
                  ReceiveLine.Invoke( this, new ReceiveLineArgs( line ) );
                  lastIndex = index + Environment.NewLine.Length;
                  index = content.IndexOf( Environment.NewLine, lastIndex );
               }
               if( lastIndex > 0 )
                  m_stringBuilder.Remove( 0, lastIndex );
            }
            m_socket.BeginReceive( m_buffer, 0, m_buffer.Length, 0, ReadCallback, null );
         }
         catch( SocketException e )
         {
            Error.Invoke( this, new ErrorEventArgs( e ) );
         }
      }

      private void SendCallback( IAsyncResult asyncResult )
      {
         CheckDisposed();
         try
         {
            int bytesSent = m_socket.EndSend( asyncResult );
         }
         catch( SocketException exception )
         {
            Error.Invoke( this, new ErrorEventArgs( exception ) );
         }
      }

      protected override void FreeManaged()
      {
         m_socket.Close();
      }

      protected override void FreeUnmanaged()
      {
      }
   }
}
