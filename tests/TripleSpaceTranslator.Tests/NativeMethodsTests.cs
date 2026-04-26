using System.Runtime.InteropServices;
using TripleSpaceTranslator.Core.Interop;

namespace TripleSpaceTranslator.Tests;

public sealed class NativeMethodsTests
{
    [Fact]
    public void InputStruct_HasExpectedWin32Size()
    {
        var expectedSize = IntPtr.Size == 8 ? 40 : 28;

        Assert.Equal(expectedSize, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
