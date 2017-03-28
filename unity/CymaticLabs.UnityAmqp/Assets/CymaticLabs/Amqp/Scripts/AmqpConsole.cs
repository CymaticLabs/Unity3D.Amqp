using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace CymaticLabs.Unity3D.Amqp.UI
{
    /// <summary>
    /// A management console for AMQP functionality.
    /// </summary>
    public class AmqpConsole : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// Whether or not the console should be open on start.
        /// </summary>
        public bool OpenOnStart = true;

        /// <summary>
        /// Whether or not to give focus to the user input of the console on start.
        /// </summary>
        public bool FocusOnStart = true;

        /// <summary>
        /// The key used to toggle the console open/closed.
        /// </summary>
        public KeyCode ToggleKey = KeyCode.F1;

        /// <summary>
        /// The console window.
        /// </summary>
        public GameObject Window;

        /// <summary>
        /// The console text area.
        /// </summary>
        public Text Text;

        /// <summary>
        /// The console input field.
        /// </summary>
        public InputField UserInput;

        /// <summary>
        /// The scroll rectangle area for the console.
        /// </summary>
        public ScrollRect ScrollArea;

        #endregion Inspector

        #region Fields

        // A list of registered console commands
        static Dictionary<string, ConsoleCommand> commands;

        // A list of the help/description for each command
        static Dictionary<string, string> commandInfos;

        // A history of all the entered console input
        static List<string> consoleHistory;

        // The current console history index
        static int historyIndex = 0;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the static instance of the console.
        /// </summary>
        public static AmqpConsole Instance { get; private set; }

        /// <summary>
        /// Gets whether or not the console is currently open.
        /// </summary>
        public static bool IsOpen { get; private set; }

        /// <summary>
        /// Gets or sets the current console color (NULL is default).
        /// </summary>
        public static Color? Color { get; set; }

        #endregion Properties

        #region Constructors

        #endregion Constructors

        #region Methods

        #region Init

        void Awake()
        {
            Instance = this; // assign static reference

            commands = new Dictionary<string, ConsoleCommand>();
            commandInfos = new Dictionary<string, string>();
            consoleHistory = new List<string>();

            // Register all console commands
            foreach (var type in Assembly.GetAssembly(GetType()).GetTypes())
            {
                foreach (var mi in type.GetMethods())
                {
                    var commandAttr = mi.GetCustomAttributes(typeof(ConsoleCommandAttribute), true).FirstOrDefault() as ConsoleCommandAttribute;
                    if (commandAttr == null) continue;
                    var command = System.Delegate.CreateDelegate(typeof(ConsoleCommand), this, mi.Name) as ConsoleCommand;
                    AddCommand(commandAttr.Name, command);
                    commandInfos.Add(commandAttr.Name, commandAttr.Description);
                }
            }

            // Import saved history
            var savedHistory = PlayerPrefs.GetString("ConsoleHistory");

            if (!string.IsNullOrEmpty(savedHistory))
            {
                var entries = savedHistory.Split(new char[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

                foreach (var entry in entries)
                {
                    if (!string.IsNullOrEmpty(entry))
                    {
                        consoleHistory.Add(SanitizeInput(entry));
                    }
                }

                // Move index to the end
                historyIndex = consoleHistory.Count;
            }
        }

        void Start()
        {
            if (OpenOnStart) Open(FocusOnStart);
        }

        #endregion Init

        #region Update

        private void Update()
        {
            if (Input.GetKeyUp(ToggleKey)) Toggle();
        }

        void LateUpdate()
        {
            try
            {
                if (!IsOpen) return;

                // Update console history
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    if (consoleHistory.Count > 0 && historyIndex - 1 >= 0)
                    {
                        historyIndex--;
                        UserInput.ActivateInputField();
                        UserInput.text = consoleHistory[historyIndex];
                        StartCoroutine("MoveCaretToEnd");
                    }
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    if (consoleHistory.Count > 0 && historyIndex + 1 < consoleHistory.Count)
                    {
                        historyIndex++;
                        UserInput.ActivateInputField();
                        UserInput.text = consoleHistory[historyIndex];
                        StartCoroutine("MoveCaretToEnd");
                    }
                    else if (consoleHistory.Count > 0)
                    {
                        if (historyIndex != consoleHistory.Count) historyIndex = consoleHistory.Count;
                        UserInput.ActivateInputField();
                        UserInput.text = "";
                        StartCoroutine("MoveCaretToEnd");
                    }
                }

                if (Input.GetKeyDown(KeyCode.Return))
                {
                    // Get the input
                    var input = SanitizeInput(UserInput.text);

                    if (!string.IsNullOrEmpty(input))
                    {
                        // Record the history
                        consoleHistory.Add(input);
                        historyIndex = consoleHistory.Count;

                        // Save the last 10 entries to player preferences
                        var sb = new System.Text.StringBuilder();
                        var start = consoleHistory.Count > 30 ? consoleHistory.Count - 31 : 0;

                        for (var i = start; i < consoleHistory.Count; i++)
                        {
                            sb.Append(consoleHistory[i] + "\n");
                        }

                        PlayerPrefs.SetString("ConsoleHistory", sb.ToString());

                        // Is this a command?
                        if (input.StartsWith("/"))
                        {
                            // Parse the arguments
                            sb = new StringBuilder();
                            var args = new Queue<string>();
                            string command = null;
                            var isEscaped = false; // whether or not the arg is an escaped string

                            for (var i = 0; i < input.Length; i++)
                            {
                                // We're at white space
                                if ((input[i] == ' ' && !isEscaped) || i == input.Length - 1)
                                {
                                    // Register the command
                                    if (command == null)
                                    {
                                        if (input[i] != ' ' && input[i] != '"' && (byte)input[i] != 10) sb.Append(input[i]);
                                        command = SanitizeInput(sb.ToString());
                                        sb = new StringBuilder();
                                    }
                                    // Parse arguments
                                    else
                                    {
                                        var value = sb.ToString();
                                        if (string.IsNullOrEmpty(value) && input[i] != ' ') value = input[i].ToString();
                                        else if (input[i] != ' ' && input[i] != '"' && (byte)input[i] != 10) value += input[i].ToString();

                                        if (!string.IsNullOrEmpty(value))
                                        {
                                            args.Enqueue(SanitizeInput(value));
                                            if (i < input.Length - 1) sb = new StringBuilder();
                                        }
                                    }
                                }
                                // Keep parsing the next token
                                else
                                {
                                    if (!isEscaped && input[i] == '"')
                                    {
                                        isEscaped = true;
                                    }
                                    else if (isEscaped && input[i] == '"')
                                    {
                                        isEscaped = false;
                                        var value = sb.ToString();

                                        if (!string.IsNullOrEmpty(value) && value != " ")
                                        {
                                            args.Enqueue(SanitizeInput(value));
                                            if (i < input.Length - 1) sb = new StringBuilder();
                                        }
                                    }
                                    else
                                    {
                                        if ((byte)input[i] != 10)
                                        {
                                            sb.Append(input[i]);
                                        }
                                    }
                                }
                            }

                            // If the command exists, execute it
                            if (commands.ContainsKey(command))
                            {
                                var cmdReturn = commands[command](args);
                                if (!string.IsNullOrEmpty(cmdReturn)) Text.text += cmdReturn + "\n";
                            }
                            else
                            {
                                Text.text += "command not found: <color=aqua>" + command + "</color>\n";
                                ScrollArea.verticalScrollbar.value = 0;
                            }
                        }
                        // Not a command
                        else
                        {
                            // Assume no default behavior
                        }

                        UserInput.text = "";
                        UserInput.Select();
                        UserInput.ActivateInputField();
                    }
                }
            }
            catch (System.Exception ex)
            {
                WriteLine(string.Format("{0}", ex));
            }
        }

        // Moves the input caret to th end of the line
        IEnumerator MoveCaretToEnd()
        {
            yield return 0;
            UserInput.MoveTextEnd(false);
        }

        #endregion Update

        #region Open/Close

        /// <summary>
        /// Opens the console.
        /// </summary>
        /// <param name="focus">Whether or not to put focus into the user input after opening.</param>
        public static void Open(bool focus = true)
        {
            if (IsOpen) return; // nothing to do
            Instance.Window.SetActive(true);
            Cursor.visible = true;
            IsOpen = true;

            if (focus)
            {
                Instance.UserInput.Select();
                Instance.UserInput.ActivateInputField();
            }
        }

        public void OnOpen()
        {
            Open();
        }

        /// <summary>
        /// Closes the console.
        /// </summary>
        public static void Close()
        {
            if (!IsOpen) return; // nothing to do
            Instance.Window.SetActive(false);
            IsOpen = false;
        }

        public void OnClose()
        {
            Close();
        }

        /// <summary>
        /// Toggles the console's open/closed state.
        /// </summary>
        public static void Toggle()
        {
            if (!IsOpen) Open();
            else Close();
        }

        #endregion Open/Close

        #region Commands

        /// <summary>
        /// Adds a new command to the console.
        /// </summary>
        /// <param name="name">The name of the command.</param>
        /// <param name="command">The commands callback handler.</param>
        public static void AddCommand(string name, ConsoleCommand command)
        {
            if (!name.StartsWith("/")) name = "/" + name;
            if (commands.ContainsKey(name)) throw new System.ArgumentException("Command already added: " + name);
            commands.Add(name, command);
        }

        /// <summary>
        /// Removes a command from the console.
        /// </summary>
        /// <param name="name">The name of the command.</param>
        public static void RemoveCommand(string name)
        {
            if (!name.StartsWith("/")) name = "/" + name;
            if (!commands.ContainsKey(name)) throw new System.ArgumentException("Command not found: " + name);
            commands.Remove(name);
        }

        #region /help

        /// <summary>
        /// Lists the available commands.
        /// </summary>
        [ConsoleCommand(Name = "/help", Description = "Lists all of the available console commands.")]
        public string CommandHelp(Queue<string> args)
        {
            var sb = new StringBuilder();
            sb.Append("\nListing available commands...\n\n");

            var c = 0;

            foreach (var pair in commands)
            {
                sb.AppendFormat("<color=aqua>{0}</color>\n{1}\n{2}", pair.Key, commandInfos[pair.Key], ++c == commands.Count ? "" : "\n");
            }

            return sb.ToString();
        }

        #endregion /help

        #region /clear

        /// <summary>
        /// Clears the console text area.
        /// </summary>
        [ConsoleCommand(Name = "/clear", Description = "Clears the console.")]
        public string CommandClear(Queue<string> args)
        {
            Text.text = "";
            return null;
        }

        #endregion /clear

        #region /quit

        ///// <summary>
        ///// Quits the game.
        ///// </summary>
        //[ConsoleCommand(Name = "/quit", Description = "Quits the game.")]
        //public string CommandQuit(Queue<string> args)
        //{
        //    // Quit the game
        //    Application.Quit();
        //    return null;
        //}

        //IEnumerator DelayedQuit(float delay)
        //{
        //    yield return new WaitForSeconds(delay);
        //    Application.Quit();
        //    yield break;
        //}

        #endregion /quit

        #region /publish

        /// <summary>
        /// Publishes an AMQP message on a particular exchange.
        /// </summary>
        [ConsoleCommand(Name = "/publish", Description = "Publishes an AMQP message on a particular exchange.")]
        public string CommandPublish(Queue<string> args)
        {
            // Validate
            if (args.Count < 2)
            {
                return "<color=red>wrong number of arguments</color>\nusage: <color=aqua>/publish {exchange name} {routing key|optional} {message}</color>\nuse double quotes to escape spaces in argument values";
            }

            // Get arguments
            string exchangeName = args.Dequeue();
            string routingKey = args.Count > 2 ? args.Dequeue() : "";
            string message = args.Dequeue();

            // Publish
            if (!AmqpClient.Instance.IsConnected)
            {
                return "<color=red>Must be connected to an AMQP broker in order to publish</color>";
            }
            else
            {
                AmqpClient.Publish(exchangeName, routingKey, message);
            }

            return null;
        }

        IEnumerator DelayedQuit(float delay)
        {
            yield return new WaitForSeconds(delay);
            Application.Quit();
            yield break;
        }

        #endregion /publish

        #endregion Commands

        #region Writing

        /// <summary>
        /// Sanitizes input text to remove non-crossplatform line endings.
        /// </summary>
        /// <param name="text">The text to sanitize.</param>
        /// <returns>The sanitized text string.</returns>
        public static string SanitizeInput(string text)
        {
            if (text == null || text.Length == 1) return text;
            return (byte)text[text.Length - 1] == 10 ? text.Substring(0, text.Length - 1) : text;
        }

        /// <summary>
        /// Writes a value to the console.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public static void Write(object value)
        {
            Write(value, false);
        }

        /// <summary>
        /// Writes a value to the console.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="timestamp">Whether or not to include a time stamp.</param>
        public static void Write(object value, bool timestamp)
        {
            Color c = new Color(1, 1, 1);
            if (Color != null) c = (Color)Color;
            var text = string.Format("<color={0}>{1}</color>", ToRGBHex(c), value);
            var time = timestamp ? "[" + System.DateTime.Now.ToLongTimeString() + "] " : "";
            Instance.Text.text += time + text;
            Instance.ScrollArea.verticalScrollbar.value = 0;
        }

        /// <summary>
        /// Writes a value to the console.
        /// </summary>
        /// <param name="text">The text string to format.</param>
        /// <param name="values">The format arguments.</param>
        public static void WriteFormat(string text, params object[] values)
        {
            Write(string.Format(text, values), false);
        }

        /// <summary>
        /// Writes a value to the console.
        /// </summary>
        /// <param name="text">The text string to format.</param>
        /// <param name="timestamp">Whether or not to include a time stamp.</param>
        /// <param name="values">The format arguments.</param>
        public static void WriteFormat(string text, bool timeStamp, params object[] values)
        {
            Write(string.Format(text, values), timeStamp);
        }

        /// <summary>
        /// Writes a value on a new line in the console.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public static void WriteLine(object value)
        {
            WriteLine(value, false);
        }

        /// <summary>
        /// Writes a value on a new line in the console.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="timestamp">Whether or not to include a time stamp.</param>
        public static void WriteLine(object value, bool timestamp)
        {
            if (Instance == null) return;
            Color c = new Color(1, 1, 1);
            if (Color != null) c = (Color)Color;
            var text = string.Format("<color={0}>{1}</color>", ToRGBHex(c), value);
            var time = timestamp ? "[" + System.DateTime.Now.ToLongTimeString() + "] " : "";
            Instance.Text.text += time + text + "\n";
            Instance.ScrollArea.verticalScrollbar.value = 0;
        }

        /// <summary>
        /// Writes a value on a new line in the console.
        /// </summary>
        /// <param name="text">The text string to format.</param>
        /// <param name="values">The format arguments.</param>
        public static void WriteLineFormat(string text, params object[] values)
        {
            WriteLine(string.Format(text, values), false);
        }

        /// <summary>
        /// Writes a value on a new line in the console.
        /// </summary>
        /// <param name="text">The text string to format.</param>
        /// <param name="timestamp">Whether or not to include a time stamp.</param>
        /// <param name="values">The format arguments.</param>
        public static void WriteLineFormat(string text, bool timeStamp, params object[] values)
        {
            WriteLine(string.Format(text, values), timeStamp);
        }

        /// <summary>
        /// Gives focus to the console.
        /// </summary>
        public void Focus()
        {
            if (!IsOpen) return;
            UserInput.Select();
            UserInput.ActivateInputField();
        }

        #endregion Writing

        #region Utility

        /// <summary>
        /// Converts an RBG color to a hexidecimal string.
        /// </summary>
        /// <param name="c">The color to convert.</param>
        /// <returns>The color as a hexidecimal string.</returns>
        public static string ToRGBHex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", ToByte(c.r), ToByte(c.g), ToByte(c.b));
        }

        private static byte ToByte(float f)
        {
            f = Mathf.Clamp01(f);
            return (byte)(f * 255);
        }

        #endregion Utility

        #endregion Methods
    }

    /// <summary>
    /// Delegate for console command handlers.
    /// </summary>
    /// <param name="args">The console arguments.</param>
    /// <returns>Any optional string value back to the console.</returns>
    public delegate string ConsoleCommand(Queue<string> args);

    /// <summary>
    /// Attribute to register a console command.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class ConsoleCommandAttribute : System.Attribute
    {
        /// <summary>
        /// The name of the console command.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The description of the console command.
        /// </summary>
        public string Description { get; set; }
    }
}

