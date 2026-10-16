# Experience, Levels, and Skill Checks

The level cap is 50. Experience to the next level is `200 + 75 × level + 15 × level²`. Every level awards `8 + floor(permanent Intellect / 2)` skill points. Even levels award one perk point. Levels 10, 20, 30, 40, and 50 award one attribute point.

Awards carry stable IDs and repeat policies. A ledger prevents quest, discovery, and interaction rewards from being credited twice.

Skill checks compare effective skill against a bounded difficulty. Margins of 0 or more succeed. Margins from -10 through -1 can become costly success only when the interaction explicitly supports it. Modifiers and explanations are sorted and preserved.
