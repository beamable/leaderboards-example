# leaderboards-example
Beamable Leaderboards example with minimalist design.

**Tournament Group Leaderboard:**
  - Individual users' tournament scores are posted in the group.
  - The overall group score is calculated as the sum of all users' scores in that specific group.
  - Each cycle has its own Group Leaderboard.
**Event Group Leaderboard:**
  - Individual users' event scores are posted in the group.
  - The overall group score is calculated as the sum of all users' scores in that specific group.
  - Each event has its own Group Leaderboard.
**Total Power Leaderboard:**
  - Individual users' card collection scores are posted in the leaderboard.
  - The overall group score is calculated as the sum of all users' scores in that specific group.
    

***Requirements:***

*Scoring Entity:*
  - The scoring entity is the group, with partitioning support based on quantity.
    
*Leaderboard Types:*
  - standard leaderboards and tournament-type leaderboards (based on cycles, where each cycle has its own leaderboard).

*Rewarding System:*
   - Group leaderboards will have a rewarding system.

*Score Updates:*
  - Scores are updatable by every member of the group, as well as through C# MS.
  - Scores can increase or decrease based on group joining/leaving

*Banning/Unbanning of Groups:*
  - the ability to ban or unban groups.
