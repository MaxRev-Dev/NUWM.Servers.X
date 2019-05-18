
#region fallen
/*
var match = new Regex(takepart, RegexOptions.ECMAScript).Matches(part);
List<string> alldist = new List<string>
{
    match[0].Value,
    match[1].Value
};

var t = part.Split(alldist.ToArray(), StringSplitOptions.None);
match = new Regex(sptitle, RegexOptions.ECMAScript).Matches(t[0]);
var firstval = part.Split(match[0].Value, StringSplitOptions.RemoveEmptyEntries);
var fst = firstval[1].Split(alldist.ToArray(), StringSplitOptions.RemoveEmptyEntries);
var y = new Regex(p_code, RegexOptions.ECMAScript).Matches(fst[0]);
var x = new Regex(coefs_names, RegexOptions.ECMAScript).Matches(fst[1]);
var s = new Regex(coefs, RegexOptions.ECMAScript).Matches(fst[1]);

List<Tuple<string, string>> tp = new List<Tuple<string, string>>();

for (int i = 0; i < (x.Count < s.Count ? x.Count : s.Count); i++)
{
    if (x[i] != null && s[i] != null)
        tp.Add(new Tuple<string, string>(x[i].Value, s[i].Value));
}

x = new Regex(coefs_names, RegexOptions.ECMAScript).Matches(fst[2]);
s = new Regex(coefs, RegexOptions.ECMAScript).Matches(fst[2]);

List<Tuple<string, string>> tp2 = new List<Tuple<string, string>>();
for (int i = 0; i < (x.Count < s.Count ? x.Count : s.Count); i++)
{
    if (x[i] != null && s[i] != null)
        tp2.Add(new Tuple<string, string>(x[i].Value, s[i].Value));
}
*/
#endregion