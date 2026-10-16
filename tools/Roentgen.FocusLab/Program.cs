using Roentgen.Focus;
Console.WriteLine("Roentgen FocusLab — Phase 11");
string command=args.FirstOrDefault()??"help";
Console.WriteLine(command switch {"targets"=>"Target graph inspection ready.","preview"=>"Deterministic forecast pipeline ready.","plan"=>"Draft/commit validation ready.","execute"=>"Slow-time execution adapter ready.","soak"=>"Run through the test project or CI for seeded soak validation.",_=>"Commands: targets, preview, plan, execute, soak"});
Console.WriteLine($"Focus perk definitions: {Phase11FocusPerks.All.Count}");
