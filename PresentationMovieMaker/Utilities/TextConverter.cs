using System;
using System.Runtime.InteropServices;

namespace PresentationMovieMaker.Utilities
{
    public class TextConverter : IDisposable
    {
        // IFELanguage2 Interface ID
        //[Guid("21164102-C24A-11d1-851A-00C04FCC6B14")]
        [ComImport]
        [Guid("019F7152-E6DB-11d0-83C3-00C04FDDB82E")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IFELanguage
        {
            int Open();
            int Close();
            int GetJMorphResult(uint dwRequest, uint dwCMode, int cwchInput, [MarshalAs(UnmanagedType.LPWStr)] string pwchInput, IntPtr pfCInfo, out object ppResult);
            int GetConversionModeCaps(ref uint pdwCaps);
            int GetPhonetic([MarshalAs(UnmanagedType.BStr)] string @string, int start, int length, [MarshalAs(UnmanagedType.BStr)] out string result);
            int GetConversion([MarshalAs(UnmanagedType.BStr)] string @string, int start, int length, [MarshalAs(UnmanagedType.BStr)] out string result);
        }

        private readonly IFELanguage _ifelang;

        public TextConverter()
        {
            var type = Type.GetTypeFromProgID("MSIME.Japan") ?? throw new Exception();
            _ifelang = Activator.CreateInstance(type) as IFELanguage ?? throw new Exception();
            int hr = _ifelang.Open();
            if (hr != 0)
            {
                throw Marshal.GetExceptionForHR(hr) ?? throw new Exception($"{hr} is not error");
            }
        }

        public void Dispose()
        {
            _ifelang?.Close();
        }

        public string ToHiragana(string source)
        {
            int hr = _ifelang.GetPhonetic(source, 1, -1, out string yomigana);
            if (hr != 0)
            {
                throw Marshal.GetExceptionForHR(hr) ?? throw new Exception($"{hr} is not error");
            }
            return yomigana;
        }
    }
}
