using System.Text;
using DotNetVault.LockedResources;

namespace ExampleCodePlayground
{
    class Bug76DemoHelper
    {
        //SHOULD NOT AND DOES NOT WORK bug 76 FIXED NOW SHOULD NOT WORK AND IN FACT DOES NOT WORK 
        //bug 76 fix uncommenting following line now rightly causes compilation failure
        //public static VaultAction<StringBuilder, string> AppendTextDelegate { get; } = StaticAppendText;

        //SHOULD NOT BUT DOES WORK BUG 76 FIXED NOW SHOULD NOT WORK AND IN FACT DOES NOT WORK
        public static VaultAction<StringBuilder, string> AppendTextDel2 { get; } =
            delegate(ref StringBuilder sb, in string s)
            {
                sb.AppendLine(s);
                //bug 76 following line uncommented now (rightly) causes compilation failure
                //StaticStringBuilder = sb;
            };


        //SHOULD NOT BUT DOES WORK BUG 76 FIXED NOW SHOULD NOT WORK AND IN FACT DOES NOT WORK
        public static VaultAction<StringBuilder, string> AppendTextDel3 { get; } =
            (ref StringBuilder sb, in string s) =>
            {
                sb.AppendLine(s);
                //bug 76 following line uncommented now (rightly) causes compilation failure
                //StaticStringBuilder = s;
            };

        public static string StaticGetText(in StringBuilder sb)
        {
            StaticStringBuilder = sb;
            return sb.ToString();
        }
        public static void StaticAppendText(ref StringBuilder sb, in string appendMe)
        {
            StaticStringBuilder = sb;
            sb.AppendLine(appendMe);
        }
        private static StringBuilder StaticStringBuilder = new StringBuilder();
    }
}
