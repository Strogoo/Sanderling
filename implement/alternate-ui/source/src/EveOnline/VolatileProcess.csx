#r "sha256:FE8A38EBCED27A112519023A7A1216C69FE0863BCA3EF766234E972E920096C1"
#r "sha256:5229128932E6AAFB5433B7AA5E05E6AFA3C19A929897E49F83690AB8FE273162"
#r "sha256:CADE001866564D185F14798ECFD077EDA6415E69D978748C19B98DDF0EE839BB"
#r "sha256:831EF0489D9FA85C34C95F0670CC6393D1AD9548EE708E223C1AD87B51F7C7B3"
#r "sha256:B9B4E633EA6C728BAD5F7CBBEF7F8B842F7E10181731DBE5EC3CD995A6F60287"
#r "sha256:81110D44256397F0F3C572A20CA94BB4C669E5DE89F9348ABAD263FBD81C54B9"

// https://github.com/Arcitectus/Sanderling/releases/download/v2025-04-20/read-memory-64-bit-separate-assemblies-abca4076e6a162e8159e604a470271c69e60b34e-win-x64.zip
#r "sha256:418f612244b2e5463751a1d049cc353f96c7b6b255b74f900d057a246a409889"

#r "mscorlib"
#r "netstandard"
#r "System"
#r "System.Collections.Immutable"
#r "System.ComponentModel.Primitives"
#r "System.IO.Compression"
#r "System.Net"
#r "System.Net.WebClient"
#r "System.Private.Uri"
#r "System.Linq"
#r "System.Security.Cryptography.Algorithms"
#r "System.Security.Cryptography.Primitives"

//  "System.Drawing.Common"
// https://www.nuget.org/api/v2/package/System.Drawing.Common/9.0.4
#r "sha256:144bc126a785601c27754cde054c2423179ebca3f734dac2b0e98738f3b59bee"

//  "System.Drawing.Primitives"
#r "sha256:CA24032E6D39C44A01D316498E18FE9A568D59C6009842029BC129AA6B989BCD"

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.InteropServices;


int readingFromGameCount = 0;
static var generalStopwatch = System.Diagnostics.Stopwatch.StartNew();

var readingFromGameHistory = new Queue<ReadingFromGameClient>();


string ToStringBase16(byte[] array) => BitConverter.ToString(array).Replace("-", "");


var searchUIRootAddressTasks = new Dictionary<int, SearchUIRootAddressTask>();

class SearchUIRootAddressTask
{
    public Request.SearchUIRootAddressStructure request;

    public TimeSpan beginTime;

    public Response.SearchUIRootAddressCompletedStruct completed;

    public SearchUIRootAddressTask(Request.SearchUIRootAddressStructure request)
    {
        this.request = request;
        beginTime = generalStopwatch.Elapsed;

        System.Threading.Tasks.Task.Run(() =>
        {
            var uiTreeRootAddress = FindUIRootAddressFromProcessId(request.processId);

            completed = new Response.SearchUIRootAddressCompletedStruct
            {
                uiRootAddress = uiTreeRootAddress?.ToString()
            };
        });
    }
}

struct ReadingFromGameClient
{
    public IntPtr windowHandle;

    public string readingId;
}

class Request
{
    public object ListGameClientProcessesRequest;

    public SearchUIRootAddressStructure SearchUIRootAddress;

    public ReadFromWindowStructure ReadFromWindow;

    public TaskOnWindow<EffectSequenceElement[]> EffectSequenceOnWindow;

    public class SearchUIRootAddressStructure
    {
        public int processId;
    }

    public class ReadFromWindowStructure
    {
        public string windowId;

        public ulong uiRootAddress;
    }

    public class TaskOnWindow<Task>
    {
        public string windowId;

        public bool bringWindowToForeground;

        public Task task;
    }

    public class EffectSequenceElement
    {
        public EffectOnWindowStructure effect;

        public int? delayMilliseconds;
    }

    public class EffectOnWindowStructure
    {
        public MouseMoveToStructure MouseMoveTo;

        public KeyboardKey KeyDown;

        public KeyboardKey KeyUp;
    }

    public class KeyboardKey
    {
        public int virtualKeyCode;
    }

    public class MouseMoveToStructure
    {
        public Location2d location;
    }

    public enum MouseButton
    {
        left, right,
    }
}

class Response
{
    public GameClientProcessSummaryStruct[] ListGameClientProcessesResponse;

    public SearchUIRootAddressResponseStruct SearchUIRootAddressResponse;

    public ReadFromWindowResultStructure ReadFromWindowResult;

    public string FailedToBringWindowToFront;

    public object CompletedEffectSequenceOnWindow;

    public object CompletedOtherEffect;

    public class GameClientProcessSummaryStruct
    {
        public int processId;

        public string mainWindowId;

        public string mainWindowTitle;

        public int mainWindowZIndex;
    }

    public class SearchUIRootAddressResponseStruct
    {
        public int processId;

        public SearchUIRootAddressStage stage;
    }


    public class SearchUIRootAddressStage
    {
        public SearchUIRootAddressInProgressStruct SearchUIRootAddressInProgress;

        public SearchUIRootAddressCompletedStruct SearchUIRootAddressCompleted;
    }

    public class SearchUIRootAddressInProgressStruct
    {
        public long searchBeginTimeMilliseconds;

        public long currentTimeMilliseconds;
    }


    public class SearchUIRootAddressCompletedStruct
    {
        public string uiRootAddress;
    }

    public class ReadFromWindowResultStructure
    {
        public object ProcessNotFound;

        public CompletedStructure Completed;

        public class CompletedStructure
        {
            public int processId;

            public Location2d windowClientRectOffset;

            public string readingId;

            public string memoryReadingSerialRepresentationJson;
        }
    }
}

public struct Location2d
{
    public Int64 x, y;
}

string serialRequest(string serializedRequest)
{
    var requestStructure = Newtonsoft.Json.JsonConvert.DeserializeObject<Request>(serializedRequest);

    var response = request(requestStructure);

    return SerializeToJsonForBot(response);
}

Response request(Request request)
{
    SetProcessDPIAware();

    if (request.ListGameClientProcessesRequest != null)
    {
        return new Response
        {
            ListGameClientProcessesResponse =
                ListGameClientProcesses().ToArray(),
        };
    }

    if (request.SearchUIRootAddress != null)
    {
        searchUIRootAddressTasks.TryGetValue(request.SearchUIRootAddress.processId, out var searchTask);

        if (searchTask is null)
        {
            searchTask = new SearchUIRootAddressTask(request.SearchUIRootAddress);

            searchUIRootAddressTasks[request.SearchUIRootAddress.processId] = searchTask;
        }

        return new Response
        {
            SearchUIRootAddressResponse = new Response.SearchUIRootAddressResponseStruct
            {
                processId = request.SearchUIRootAddress.processId,
                stage = SearchUIRootAddressTaskAsResponseStage(searchTask)
            },
        };
    }

    if (request.ReadFromWindow != null)
    {
        var readingFromGameIndex = System.Threading.Interlocked.Increment(ref readingFromGameCount);

        var readingId = readingFromGameIndex.ToString("D6") + "-" + generalStopwatch.ElapsedMilliseconds;

        var windowId = request.ReadFromWindow.windowId;
        var windowHandle = new IntPtr(long.Parse(windowId));

        WinApi.GetWindowThreadProcessId(windowHandle, out var processIdUnsigned);

        if (processIdUnsigned == 0)
            return new Response
            {
                ReadFromWindowResult = new Response.ReadFromWindowResultStructure
                {
                    ProcessNotFound = new object(),
                }
            };

        var processId = (int)processIdUnsigned;

        var windowRect = new WinApi.Rect();
        WinApi.GetWindowRect(windowHandle, ref windowRect);

        var clientRectOffsetFromScreen = new WinApi.Point(0, 0);
        WinApi.ClientToScreen(windowHandle, ref clientRectOffsetFromScreen);

        var windowClientRectOffset =
            new Location2d
            { x = clientRectOffsetFromScreen.x - windowRect.left, y = clientRectOffsetFromScreen.y - windowRect.top };

        string memoryReadingSerialRepresentationJson = null;

        using (var memoryReader = new read_memory_64_bit.MemoryReaderFromLiveProcess(processId))
        {
            var uiTree = read_memory_64_bit.EveOnline64.ReadUITreeFromAddress(request.ReadFromWindow.uiRootAddress, memoryReader, 99);

            if (uiTree != null)
            {
                memoryReadingSerialRepresentationJson =
                read_memory_64_bit.EveOnline64.SerializeMemoryReadingNodeToJson(
                    uiTree.WithOtherDictEntriesRemoved());
            }
        }

        {
            /*
            Maybe taking screenshots needs the window to be not occluded by other windows.
            We can review this later.
            */
            var setForegroundWindowError = SetForegroundWindowInWindows.TrySetForegroundWindow(windowHandle);

            if (setForegroundWindowError != null)
            {
                return new Response
                {
                    FailedToBringWindowToFront = setForegroundWindowError,
                };
            }
        }

        var historyEntry = new ReadingFromGameClient
        {
            windowHandle = windowHandle,
            readingId = readingId
        };

        readingFromGameHistory.Enqueue(historyEntry);

        while (4 < readingFromGameHistory.Count)
        {
            readingFromGameHistory.Dequeue();
        }

        return new Response
        {
            ReadFromWindowResult = new Response.ReadFromWindowResultStructure
            {
                Completed = new Response.ReadFromWindowResultStructure.CompletedStructure
                {
                    processId = processId,
                    windowClientRectOffset = windowClientRectOffset,
                    memoryReadingSerialRepresentationJson = memoryReadingSerialRepresentationJson,
                    readingId = readingId
                },
            },
        };
    }

    if (request?.EffectSequenceOnWindow?.task != null)
    {
        var windowHandle = new IntPtr(long.Parse(request.EffectSequenceOnWindow.windowId));

        if (request.EffectSequenceOnWindow.bringWindowToForeground)
        {
            var setForegroundWindowError = SetForegroundWindowInWindows.TrySetForegroundWindow(windowHandle);

            if (setForegroundWindowError != null)
            {
                return new Response
                {
                    FailedToBringWindowToFront = setForegroundWindowError,
                };
            }
        }

        foreach (var sequenceElement in request.EffectSequenceOnWindow.task)
        {
            if (sequenceElement?.effect != null)
                ExecuteEffectOnWindow(sequenceElement.effect, windowHandle, request.EffectSequenceOnWindow.bringWindowToForeground);

            if (sequenceElement?.delayMilliseconds != null)
                System.Threading.Thread.Sleep(sequenceElement.delayMilliseconds.Value);
        }

        return new Response
        {
            CompletedEffectSequenceOnWindow = new object(),
        };
    }

    return null;
}

static Response.SearchUIRootAddressStage SearchUIRootAddressTaskAsResponseStage(SearchUIRootAddressTask task)
{
    return task.completed switch
    {
        Response.SearchUIRootAddressCompletedStruct completed =>
        new Response.SearchUIRootAddressStage { SearchUIRootAddressCompleted = completed },

        _ => new Response.SearchUIRootAddressStage
        {
            SearchUIRootAddressInProgress = new Response.SearchUIRootAddressInProgressStruct
            {
                searchBeginTimeMilliseconds = (long)task.beginTime.TotalMilliseconds,
                currentTimeMilliseconds = generalStopwatch.ElapsedMilliseconds,
            }
        }
    };
}

static ulong? FindUIRootAddressFromProcessId(int processId)
{
    var candidatesAddresses =
        read_memory_64_bit.EveOnline64.EnumeratePossibleAddressesForUIRootObjectsFromProcessId(processId);

    using (var memoryReader = new read_memory_64_bit.MemoryReaderFromLiveProcess(processId))
    {
        var uiTrees =
            candidatesAddresses
            .Select(candidateAddress => read_memory_64_bit.EveOnline64.ReadUITreeFromAddress(candidateAddress, memoryReader, 99))
            .ToList();

        return
            uiTrees
            .OrderByDescending(uiTree => uiTree?.EnumerateSelfAndDescendants().Count() ?? -1)
            .FirstOrDefault()
            ?.pythonObjectAddress;
    }
}

void ExecuteEffectOnWindow(
    Request.EffectOnWindowStructure effectOnWindow,
    IntPtr windowHandle,
    bool bringWindowToForeground)
{
    if (bringWindowToForeground)
        BotEngine.WinApi.User32.SetForegroundWindow(windowHandle);

    if (effectOnWindow?.MouseMoveTo != null)
    {
        //  Build motion description based on https://github.com/Arcitectus/Sanderling/blob/ada11c9f8df2367976a6bcc53efbe9917107bfa7/src/Sanderling/Sanderling/Motor/Extension.cs#L24-L131

        var mousePosition = new Bib3.Geometrik.Vektor2DInt(
            effectOnWindow.MouseMoveTo.location.x,
            effectOnWindow.MouseMoveTo.location.y);

        var mouseButtons = new BotEngine.Motor.MouseButtonIdEnum[] { };

        var windowMotor = new Sanderling.Motor.WindowMotor(windowHandle);

        var motionSequence = new BotEngine.Motor.Motion[]{
            new BotEngine.Motor.Motion(
                mousePosition: mousePosition,
                mouseButtonDown: mouseButtons,
                windowToForeground: bringWindowToForeground),
            new BotEngine.Motor.Motion(
                mousePosition: mousePosition,
                mouseButtonUp: mouseButtons,
                windowToForeground: bringWindowToForeground),
        };

        windowMotor.ActSequenceMotion(motionSequence);
    }

    if (effectOnWindow?.KeyDown != null)
    {
        var virtualKeyCode = (WindowsInput.Native.VirtualKeyCode)effectOnWindow.KeyDown.virtualKeyCode;

        (MouseActionForKeyUpOrDown(keyCode: virtualKeyCode, buttonUp: false)
        ??
        (() => new WindowsInput.InputSimulator().Keyboard.KeyDown(virtualKeyCode)))();
    }

    if (effectOnWindow?.KeyUp != null)
    {
        var virtualKeyCode = (WindowsInput.Native.VirtualKeyCode)effectOnWindow.KeyUp.virtualKeyCode;

        (MouseActionForKeyUpOrDown(keyCode: virtualKeyCode, buttonUp: true)
        ??
        (() => new WindowsInput.InputSimulator().Keyboard.KeyUp(virtualKeyCode)))();
    }
}

static System.Action MouseActionForKeyUpOrDown(WindowsInput.Native.VirtualKeyCode keyCode, bool buttonUp)
{
    WindowsInput.IMouseSimulator mouseSimulator() => new WindowsInput.InputSimulator().Mouse;

    var method = keyCode switch
    {
        WindowsInput.Native.VirtualKeyCode.LBUTTON =>
            buttonUp ?
            (System.Func<WindowsInput.IMouseSimulator>)mouseSimulator().LeftButtonUp
            : mouseSimulator().LeftButtonDown,
        WindowsInput.Native.VirtualKeyCode.RBUTTON =>
            buttonUp ?
            (System.Func<WindowsInput.IMouseSimulator>)mouseSimulator().RightButtonUp
            : mouseSimulator().RightButtonDown,
        _ => null
    };

    if (method != null)
        return () => method();

    return null;
}

string SerializeToJsonForBot<T>(T value) =>
    Newtonsoft.Json.JsonConvert.SerializeObject(
        value,
        //  Use settings to get same derivation as at https://github.com/Arcitectus/Sanderling/blob/ada11c9f8df2367976a6bcc53efbe9917107bfa7/src/Sanderling/Sanderling.MemoryReading.Test/MemoryReadingDemo.cs#L91-L97
        new Newtonsoft.Json.JsonSerializerSettings
        {
            //  Bot code does not expect properties with null values, see https://github.com/Viir/bots/blob/880d745b0aa8408a4417575d54ecf1f513e7aef4/explore/2019-05-14.eve-online-bot-framework/src/Sanderling_Interface_20190514.elm
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,

            // https://stackoverflow.com/questions/7397207/json-net-error-self-referencing-loop-detected-for-type/18223985#18223985
            ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
        });


void SetProcessDPIAware()
{
    //  https://www.google.com/search?q=GetWindowRect+dpi
    //  https://github.com/dotnet/wpf/issues/859
    //  https://github.com/dotnet/winforms/issues/135
    WinApi.SetProcessDPIAware();
}

static public class WinApi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int x;
        public int y;

        public Point(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    static public extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    static public extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /*
    https://stackoverflow.com/questions/19867402/how-can-i-use-enumwindows-to-find-windows-with-a-specific-caption-title/20276701#20276701
    https://stackoverflow.com/questions/295996/is-the-order-in-which-handles-are-returned-by-enumwindows-meaningful/296014#296014
    */
    public static System.Collections.Generic.IReadOnlyList<IntPtr> ListWindowHandlesInZOrder()
    {
        IntPtr found = IntPtr.Zero;
        System.Collections.Generic.List<IntPtr> windowHandles = new System.Collections.Generic.List<IntPtr>();

        EnumWindows(delegate (IntPtr wnd, IntPtr param)
        {
            windowHandles.Add(wnd);

            // return true here so that we iterate all windows
            return true;
        }, IntPtr.Zero);

        return windowHandles;
    }

    [DllImport("user32.dll")]
    static public extern IntPtr ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    static public extern IntPtr GetWindowRect(IntPtr hWnd, ref Rect rect);

    [DllImport("user32.dll", SetLastError = false)]
    static public extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static public extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    static public extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    static public extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}

static public class SetForegroundWindowInWindows
{
    static public int AltKeyPlusSetForegroundWindowWaitTimeMilliseconds = 60;

    /// <summary>
    /// </summary>
    /// <param name="windowHandle"></param>
    /// <returns>null in case of success</returns>
    static public string TrySetForegroundWindow(IntPtr windowHandle)
    {
        try
        {
            /*
            * For the conditions for `SetForegroundWindow` to work, see https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow
            * */
            BotEngine.WinApi.User32.SetForegroundWindow(windowHandle);

            if (BotEngine.WinApi.User32.GetForegroundWindow() == windowHandle)
                return null;

            var windowsInZOrder = WinApi.ListWindowHandlesInZOrder();

            var windowIndex = windowsInZOrder.ToList().IndexOf(windowHandle);

            if (windowIndex < 0)
                return "Did not find window for this handle";

            {
                var simulator = new WindowsInput.InputSimulator();

                simulator.Keyboard.KeyDown(WindowsInput.Native.VirtualKeyCode.MENU);
                BotEngine.WinApi.User32.SetForegroundWindow(windowHandle);
                simulator.Keyboard.KeyUp(WindowsInput.Native.VirtualKeyCode.MENU);

                System.Threading.Thread.Sleep(AltKeyPlusSetForegroundWindowWaitTimeMilliseconds);

                if (BotEngine.WinApi.User32.GetForegroundWindow() == windowHandle)
                    return null;

                return "Alt key plus SetForegroundWindow approach was not successful.";
            }
        }
        catch (Exception e)
        {
            return "Exception: " + e.ToString();
        }
    }
}

struct Rectangle
{
    public Rectangle(Int64 left, Int64 top, Int64 right, Int64 bottom)
    {
        this.left = left;
        this.top = top;
        this.right = right;
        this.bottom = bottom;
    }

    readonly public Int64 top, left, bottom, right;

    override public string ToString() =>
        Newtonsoft.Json.JsonConvert.SerializeObject(this);
}


System.Diagnostics.Process[] GetWindowsProcessesLookingLikeEVEOnlineClient() =>
    System.Diagnostics.Process.GetProcessesByName("exefile");


System.Collections.Generic.IReadOnlyList<Response.GameClientProcessSummaryStruct> ListGameClientProcesses()
{
    var allWindowHandlesInZOrder = WinApi.ListWindowHandlesInZOrder();

    int? zIndexFromWindowHandle(IntPtr windowHandleToSearch) =>
        allWindowHandlesInZOrder
        .Select((windowHandle, index) => (windowHandle, index: (int?)index))
        .FirstOrDefault(handleAndIndex => handleAndIndex.windowHandle == windowHandleToSearch)
        .index;

    var processes =
        GetWindowsProcessesLookingLikeEVEOnlineClient()
        .Select(process =>
        {
            return new Response.GameClientProcessSummaryStruct
            {
                processId = process.Id,
                mainWindowId = process.MainWindowHandle.ToInt64().ToString(),
                mainWindowTitle = process.MainWindowTitle,
                mainWindowZIndex = zIndexFromWindowHandle(process.MainWindowHandle) ?? 9999,
            };
        })
        .ToList();

    return processes;
}

string InterfaceToHost_Request(string request)
{
    return serialRequest(request);
}
