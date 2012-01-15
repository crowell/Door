using System;
using System.Drawing;
using SdlDotNet.Graphics;
using SdlDotNet.Core;
using SdlDotNet.Input;
using System.IO.Ports; //connection to the Arduino
using System.Text; //stringbuilder
using System.Data;


/*
 * BUILDS Door Code rewritten in C#
 * Should be much more readable and maintainable 
 * Code Readability and style is most important here
 * All complaints, feature requests, etc. go to 
 * Jeff Crowell <crowell@bu.edu>
 */

public class SDL
{
    //begin members
    private static Surface mVideoScreen;
    private static Surface mBackground;
    private static Surface mForeground;
    private static Point mForegroundPosition;
    private static Surface mTextSurface;
    private static SdlDotNet.Graphics.Font mFont;
    private static SerialPort mArduino;
    private static StringBuilder mID2;
    private static bool mStringReplace;
    private static string mBackgroundPath;
    private static string mForegroundPath;

    //begin functions
    public static void Main(string[] args)
    {
        //args are the image(s) to load
        mBackgroundPath = args[0];
        mForegroundPath = args[1];
        if (args.Length > 2 && args[2] == "true") mStringReplace = true;

        //first, try to connect to arduino
        string arduino = GetArduinoSerial();
        if (!ConnectToArduino(arduino))
        {
            //Kill the program because we can't control the door like this
            System.Console.WriteLine("Could not connect to Arduino on port " + arduino);
            //Environment.Exit(1);
        }

        //select the font
        mFont = new SdlDotNet.Graphics.Font(@"font.ttf", 48);
        //set up the screen dimensions
        mVideoScreen = Video.SetVideoMode(1366/*width*/, 768/*height*/, false /*resize*/,
            false /*opengl*/, true /*fullscreen*/);
        
        //load the images or quit if they can't be loaded
        if (!LoadImages(mBackgroundPath, mForegroundPath)) Environment.Exit(1); //try to load the images, if cant, die

        Events.Fps = 30; //30 fps seems good for this application
        Events.Quit += new EventHandler<QuitEventArgs>(Events_Quit);
        //Events.Tick += new EventHandler<TickEventArgs>(Events_Tick);
        //Events.KeyboardDown += new EventHandler<KeyboardEventArgs>(Events_KeyboardDown);
        Events.KeyboardUp += new EventHandler<KeyboardEventArgs>(Events_KeyboardUp);
        PrintWelcomeMessage(); //Prints out the welcome message, should be able to grab updates from a file on disk
        Events.Run();
    }
    private static void ResetScreen()
    {
        //mVideoScreen = Video.SetVideoMode(1366/*width*/, 768/*height*/, false /*resize*/,
        //    false /*opengl*/, true /*fullscreen*/);
        
        //if (!LoadImages(mBackgroundPath, mForegroundPath)) Environment.Exit(1); //try to load the images, if cant, die
        mVideoScreen.Blit(mBackground);
        PrintWelcomeMessage();
        mVideoScreen.Update();
    }
    private static string GetArduinoSerial()
    {
        string[] ports = SerialPort.GetPortNames();
        int index = 0;
        if (ports.Length == 0) return null; //no connected serials
        foreach (string port in ports)
        {
            System.Console.WriteLine(index++ + " : " + port);
        }
        if (Int32.TryParse(System.Console.ReadLine(), out index))
        {
            if (index < ports.Length && index >= 0)
            {
                return ports[index];
            }
        }
        return GetArduinoSerial();
    }
    private static bool ConnectToArduino(string arduino)
    {
        if (arduino == null)
        {
            return false;
        }
        mArduino = new SerialPort(arduino, 9600);//9600 baud rate for the arduino
        mArduino.Open();
        if (mArduino.IsOpen) return true;
        return false;
    }
    private static void PrintWelcomeMessage()
    {
        DispSDLText(mVideoScreen, "Welcome to BUILDS", -1, 100);
        DispSDLText(mVideoScreen, "Please Swipe Your ID", -1, 150);
        DispSDLText(mVideoScreen, "testing this", -1, 200);
    }
    private static void DispSDLText(Surface screen, string text, int x, int y)
    {
        mTextSurface = mFont.Render(text, Color.Red); //hardcoded, but should look up from a location 
        if (x == -1)
        {
            x = mVideoScreen.Width / 2 - mTextSurface.Width/2 + 2;
        }
        mVideoScreen.Blit(mTextSurface, new Point(x,y));
        mVideoScreen.Update();
    }
    private static bool LoadImages(string aBackground, string aForeground)
    {
        if (!System.IO.File.Exists(aBackground) || !System.IO.File.Exists(aForeground))
        {
            System.Console.WriteLine("ERROR: could not find the images");
            return false;
        }
        mBackground = (new Surface(aBackground)).Convert(mVideoScreen, true, true);
        mForeground = (new Surface(aForeground)).Convert(mVideoScreen, true, true);
       // mForeground.Transparent = true;
       // mForeground.TransparentColor = Color.FromKnownColor(KnownColor.White);
        mForegroundPosition = new Point(mVideoScreen.Width / 2 - mForeground.Width / 2, mVideoScreen.Height / 2 - mForeground.Height / 2);
        mVideoScreen.Blit(mBackground);
        return true;
    }

    private static void Events_KeyboardUp(object sender, KeyboardEventArgs args)
    {
        if (args.Key == Key.KeypadEnter)
        {
            ResetScreen();
        }
        else if (args.Key == Key.Escape)
        {
            System.Console.WriteLine("Escape pressed, Quitting");
            Environment.Exit(0);
        }
        else if (args.EventStruct.key.keysym.scancode != 0x24)
        {
            mID2.Append(args.KeyboardCharacter);
        }

        else
        {
            string id = mID2.ToString();
            mID2 = new StringBuilder(); // zero out the mID2
            if (mStringReplace)
            {
                id = id.Substring(10, id.Length - 1);
                char[] idstr = id.ToCharArray();
                idstr[0] = 'u';
                id = idstr.ToString();
            }

            System.Data.SqlClient.SqlConnection doorDB =
                new System.Data.SqlClient.SqlConnection("user id=uid;" +
                    "password=pwrod;server=url;Trusted_Connection=yes;" +
                    "database=database;connection timeout=30");
            try
            {
                doorDB.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            string query = String.Format("SELECT id, first FROM users where swipe=\"{0}\"", id);
            System.Data.SqlClient.SqlCommand qCommand = new System.Data.SqlClient.SqlCommand(query, doorDB);
            System.Data.SqlClient.SqlDataReader qReader = null;
            StringBuilder sb = new StringBuilder("Hello ");
            try
            {
                qReader = qCommand.ExecuteReader();
                while (qReader.Read())
                {
                    sb.Append(qReader[1].ToString());
                    query = String.Format("insert into log(user,time) VALUES(\"{0}\",NOW())", qReader[0]);
                    ResetScreen();
                    DispSDLText(mVideoScreen, sb.ToString(), -1, 100);
                    //now unlock the door
                    mArduino.Write("u\r");
                    mVideoScreen.Update();
                    SdlDotNet.Core.Timer.DelaySeconds(5); //delay so to show the animation
                    ResetScreen();
                    PrintWelcomeMessage();
                }
                try
                {
                    doorDB.Close();
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine(ex.ToString());
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex.ToString());
            }
        }
    }
    private static void Events_Tick(object sender, TickEventArgs args)
    {
        //not used now
    }
    private static void Events_Quit(object sender, QuitEventArgs args)
    {
        Events.QuitApplication();
    }
}