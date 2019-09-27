using System;
using System.Collections.Generic;
using System.Linq;

/**
 * Deliver more ore to hq (left side of the map) than your opponent. Use radars to find ore but beware of traps!
 **/
class Player
{
    static void Main(string[] args)
    {
        string[] inputs;
        //the map
        inputs = Console.ReadLine().Split(' ');
        int width = int.Parse(inputs[0]);
        int height = int.Parse(inputs[1]);

        //the object of that map
        Map map = new Map(width, height);

        // game loop
        while (true)
        {
            inputs = Console.ReadLine().Split(' ');

            //feed the scores
            map.InitializeScores(int.Parse(inputs[0]), int.Parse(inputs[1]));

            //the map status
            for (int i = 0; i < height; i++)
            {
                inputs = Console.ReadLine().Split(' ');
                for (int j = 0; j < width; j++)
                {
                    var coord = map.Get(j, i);

                    string ore = inputs[2 * j];// amount of ore or "?" if unknown
                    int hole = int.Parse(inputs[2 * j + 1]);// 1 if cell has a hole

                    coord.SetType(TileType.Ore);

                    if (int.TryParse(ore, out int val))
                    {
                        coord.SetOreValue(val);
                    }

                    if (hole == 1)
                    {
                        coord.SetType(TileType.Hole);
                    }

                }
            }

            inputs = Console.ReadLine().Split(' ');
            int entityCount = int.Parse(inputs[0]); // number of entities visible to you
            int radarCooldown = int.Parse(inputs[1]); // turns left until a new radar can be requested
            map.UpdateRadarCd(radarCooldown);

            int trapCooldown = int.Parse(inputs[2]); // turns left until a new trap can be requested

            //entity check
            for (int i = 0; i < entityCount; i++)
            {
                inputs = Console.ReadLine().Split(' ');
                int id = int.Parse(inputs[0]); // unique id of the entity
                int type = int.Parse(inputs[1]); // 0 for your robot, 1 for other robot, 2 for radar, 3 for trap
                int x = int.Parse(inputs[2]);
                int y = int.Parse(inputs[3]); // position of the entity
                int item = int.Parse(inputs[4]); // if this entity is a robot, the item it is carrying (-1 for NONE, 2 for RADAR, 3 for TRAP, 4 for ORE)


                if (x != -1 && y != -1)
                {
                    var entity = map.Get(id);
                    
                    if(entity != null)
                    {
                        //we should the entities to at least retain their integrity
                        entity.Update(map.Get(x, y), item);
                    }
                    else
                    {
                        entity = new Entity(id, type, map.Get(x, y), item);
                    }

                    map.UpdateEntity(entity);
                }
                else
                {
                    map.Remove(id);
                }

            }

            //Console.Error.WriteLine(map.Entities.Count());
            //map.ProcessGreedy();
            map.ProcessSearchBased();
            //map.ProcessSearchBased2();
            foreach (var et in map.MyRobots)
            {
                // Write an action using Console.WriteLine()
                // To debug: Console.Error.WriteLine("Debug messages...");
                Console.WriteLine(et.Command); // WAIT|MOVE x y|DIG x y|REQUEST item
            }
        }
    }
}

public class Map
{
    private readonly List<Coordinate> _vectors = new List<Coordinate>();
    private readonly Dictionary<int, Entity> _entities = new Dictionary<int, Entity>();
    public int MyScore { get; private set; }
    public int EnemyScore { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public IEnumerable<Entity> Radars
    {
        get
        {
            return _entities.Values.Where(i => i.Type == EntityType.BuriedRadar);
        }
    }

    public IEnumerable<Entity> Entities
    {
        get
        {
            return _entities.Values;
        }
    }

    private bool _isRadarReady;
    public bool CanRequestRadar
    {
        get
        {
            return _isRadarReady || Radars.Count() >= 8;
        }
        set
        {
            _isRadarReady &= value;
        }
    }

    public IEnumerable<Entity> MyRobots
    {
        get
        {
            return _entities.Values.Where(v => v.IsMyRobot);
        }
    }

    public Map(int width, int height)
    {
        Width = width;
        Height = height;

        for (int h = 0; h < height; h++)
        {
            for (int w = 0; w < width; w++)
            {
                var coord = new Coordinate(w, h);
                _vectors.Add(coord);
            }
        }
    }

    public Coordinate Get(int x, int y)
    {
        return _vectors.FirstOrDefault(v => v.X == x && v.Y == y);
    }

    public void InitializeScores(int me, int enemy)
    {
        MyScore = me;
        EnemyScore = enemy;
    }

    public Entity Get(int entId)
    {
        _entities.TryGetValue(entId, out Entity entie);

        return entie;
    }

    public void UpdateEntity(Entity entity)
    {
        _entities[entity.Id] = entity;
    }

    public void ProcessGreedy()
    {
        foreach (var et in MyRobots)
        {
            if (et.Item == Item.Ore)
            {
                //run back
                et.Move(Get(0, et.PositionCurrent.Y));
                continue;
            }

            et.DigGreedy(this);
        }
    }

    public void ProcessSearchBased2()
    {
        foreach (var harvester in MyRobots)
        {
            if (!harvester.HasReachedTarget)
            {
                //do not issue other commands until we do what we queued 
                //i.e. command string should be the same until positions match or command is completed
                harvester.DoLastCommand();
                Console.Error.WriteLine($"Robot {harvester.Id} RECYCLE: {harvester.Command}");
                continue;
            }

            if (harvester.AtHome() &&
                CanRequestRadar)
            {
                //acquire radar if we can
                harvester.AcquireRadar();
                CanRequestRadar = false;
                continue;
            }

            //norma logic
            switch (harvester.Item)
            {
                case Item.Ore:
                    {
                        //go home
                        harvester.MoveHorizontal(-harvester.PositionCurrent.X);
                        continue;
                    }
                case Item.Radar:
                    {
                        //plant the radar and compute density map
                        PlantRadar(harvester);
                        continue;
                    }
                case Item.None:
                    {
                        //start digging anything nearby
                        //and head to radar point determined by map

                        //check if i'm near any point with crystals
                        //if not, head to the nearest radar
                        var radarsNear = FindRadarsByDistance(harvester).ToList();
                        var crystalNear = GetKnownOreCells().OrderBy(f => f.Distance(f)).FirstOrDefault();
                        var radarNear = radarsNear.FirstOrDefault();

                        //what if radarNear is the assigned radar?
                        //if(radarNear == harvester.AssignedRadar &&
                        //    radarsNear.Count > 1)
                        //{
                        //    //find the next nearest radar
                        //    harvester.Move(radarsNear[1].PositionCurrent);
                        //    continue;
                        //}

                        if (radarNear != null || crystalNear != null)
                        {
                            var radarDist = radarNear != null ? harvester.PositionCurrent.Distance(radarNear.PositionCurrent) : 0;
                            var crystalDist = crystalNear != null ? harvester.PositionCurrent.Distance(crystalNear) : 0;

                            if (radarDist < crystalDist)
                            {
                                if (radarNear != harvester.AssignedRadar)
                                {
                                    harvester.Move(radarNear.PositionCurrent);
                                    harvester.AssignRadar(radarNear);
                                    continue;
                                }
                                else if (radarNear == harvester.AssignedRadar && radarsNear.Count > 1)
                                {
                                    harvester.Move(radarsNear[1].PositionCurrent);
                                    harvester.AssignRadar(radarsNear[1]);
                                    continue;
                                }
                            }
                            else
                            {
                                harvester.Dig(crystalNear);
                                continue;
                            }
                        }
                        else //something else happened, dig randomly
                        {
                            var boundCoord = GetWithBound(harvester.PositionCurrent.X + 1, harvester.PositionCurrent.Y);

                            harvester.Dig(boundCoord);
                            continue;
                        }
                        continue;
                    }
                    //harvester is holding something different
            }
        }
    }

    private Coordinate GetWithBound(int x, int y)
    {
        if (x > Width)
        {
            x = Width;
        }

        if (y > Height)
        {
            y = Height;
        }

        return Get(x, y);
    }

    private IEnumerable<Coordinate> GetKnownOreCells()
    {
        return _vectors.Where(r => r.OreValue > 0);
    }

    private void PlantRadar(Entity harvester)
    {
        var mp = new Coordinate(6, 4);
        var mp2 = new Coordinate(14, 4);
        var mp3 = new Coordinate(21, 4);
        var mp4 = new Coordinate(26, 4);
        var mp5 = new Coordinate(6, 10);
        var mp6 = new Coordinate(14, 10);
        var mp7 = new Coordinate(21, 10);
        var mp8 = new Coordinate(26, 10);

        if (GetRadar(mp) == null)
        {
            harvester.Dig(mp);
        }
        else if (GetRadar(mp5) == null)
        {
            harvester.Dig(mp5);
        }
        else if (GetRadar(mp2) == null)
        {
            harvester.Dig(mp2);
        }
        else if (GetRadar(mp6) == null)
        {
            harvester.Dig(mp6);
        }
        else if (GetRadar(mp3) == null)
        {
            harvester.Dig(mp3);
        }
        else if (GetRadar(mp7) == null)
        {
            harvester.Dig(mp7);
        }
        else if (GetRadar(mp8) == null)
        {
            harvester.Dig(mp8);
        }
        else if(GetRadar(mp4) == null)
        {
            harvester.Dig(mp4);
        }
        else
        {
            //we've saturated the map!
            //drop the radar
            harvester.Dig(harvester.PositionCurrent);
        }
    }

    public void ProcessSearchBased()
    {
        //get a heat map of the ores near the radars
        //do this ONCE only
        var radars = SortRadarDataByDensest(Radars);

        //we need 1 robots with radar
        //first and last will implent greedy
        //does it need to be?
        for (int i = 0; i < MyRobots.Count(); i++)
        {
            var entity = MyRobots.ElementAt(i);

            if (entity.AtHome() && CanRequestRadar)
            {
                //ensure that we can get a radar if we need radars
                entity.AcquireRadar();
                CanRequestRadar = false;
            }
            else if (entity.Item == Item.Radar)
            {
                //drop it in the middle of a quadrant if robot has a radar
                var mp = new Coordinate(6, 4);
                var mp2 = new Coordinate(14, 4);
                var mp3 = new Coordinate(21, 4);
                var mp4 = new Coordinate(26, 4);
                var mp5 = new Coordinate(6, 10);
                var mp6 = new Coordinate(14, 10);
                var mp7 = new Coordinate(21, 10);
                var mp8 = new Coordinate(26, 10);
               
                if (GetRadar(mp) == null)
                {
                    entity.Dig(mp);
                }
                else if (GetRadar(mp5) == null)
                {
                    entity.Dig(mp5);
                }
                else if (GetRadar(mp2) == null)
                {
                    entity.Dig(mp2);
                }
                else if (GetRadar(mp6) == null)
                {
                    entity.Dig(mp6);
                }
                else if (GetRadar(mp3) == null)
                {
                    entity.Dig(mp3);
                }
                else if (GetRadar(mp7) == null)
                {
                    entity.Dig(mp7);
                }
                else if (GetRadar(mp8) == null)
                {
                    entity.Dig(mp8);
                }
                else if (GetRadar(mp4) == null)
                {
                    entity.Dig(mp4);
                }
                else
                {
                    //if all positions have radars, this logic should not be hit
                    var rdr = FindNearestOreCellByEntity(entity).FirstOrDefault();

                    if (rdr != null)
                    {
                        entity.Dig(rdr);
                    }
                    else
                    {
                        var get = FindNearestRadar(entity);

                        if (get != null)
                        {
                            entity.Dig(get.PositionCurrent);
                        }
                    }
                }
            }
            else if (entity.Item == Item.None)
            {
                //logic should be like this:
                //determine patch to be mined:
                //  can use density map based on Map tracker
                //  can use density map based on current radar
                //use position from compute step as the start
                //try to head to position
                //but if there are ANY known patches near the robot, start digging

                //find nearest radar to et
                if (radars.Count() > 1)
                {
                    //don't find the radar you mong
                    //it's supposed to be biased to the distance measurement
                    //find the patchy
                    var highest = radars.FirstOrDefault();
                    var ayy = FindRadar2(highest);

                    if (highest != null && ayy != null)
                    {
                        entity.Dig(ayy);
                    }
                    else
                    {
                        entity.DigGreedy(this);
                    }
                }
                else
                {
                    //you have the radar
                    var radar = FindNearestRadar(entity);

                    if (radar != null)
                    {
                        //from the radar to my harvester
                        //how far is the CLOSEST patch of minerals?
                        var patchOfMinerals = FindNearestOreCellByEntity(entity).FirstOrDefault();

                        if (patchOfMinerals != null && patchOfMinerals.OreValue > 0)
                        {
                            entity.Dig(patchOfMinerals);
                        }
                        else
                        {
                            entity.DigGreedy(this);
                        }
                    }
                    else
                    {
                        entity.DigGreedy(this);
                    }
                }
            }
            else if (entity.Item == Item.Ore)
            {
                //if you got ore, go home!
                entity.MoveHorizontal(-entity.PositionCurrent.X);
            }
        }
    }

    private Entity GetRadar(Coordinate mp)
    {
        var item = _entities.Values.Where(f => f.PositionCurrent.Equals(mp)).FirstOrDefault();

        if (item != null && item.Type == EntityType.BuriedRadar)
        {
            return item;
        }

        return null;
    }

    public Coordinate FindRadar2(Entity robot)
    {
        Coordinate mineralPatch = null;

        foreach (var radar in Radars)
        {
            //what's a good radar?

            var et = FindNearestDenseOreCellByArea(radar);

            if (mineralPatch == null)
            {
                mineralPatch = et;
            }
            else if (et != null && mineralPatch.OreValue < et.OreValue)
            {
                mineralPatch = et;
            }
        }

        return mineralPatch;
    }

    private IEnumerable<Entity> FindRadarsByDistance(Entity harvester)
    {
        List<Entity> radars = new List<Entity>();
        foreach (var radar in Radars)
        {
            radars.Add(radar);
        }
        //Console.Error.WriteLine($"Dist. Entity {entity.Id} : {minDist} to radar {retVal?.Id}");
        return radars.OrderBy(f => f.Distance(harvester));
    }

    private Entity FindNearestRadar(Entity entity)
    {
        float minDist = 0;
        Entity retVal = null;
        foreach (var radar in Radars)
        {
            var dist = entity.Distance(radar);
            if (dist > minDist)
            {
                minDist = dist;
                retVal = radar;
            }
        }
        //Console.Error.WriteLine($"Dist. Entity {entity.Id} : {minDist} to radar {retVal?.Id}");
        return retVal;
    }

    public Coordinate FindNearestOreCellByWidth(Entity et)
    {
        Coordinate coord = null;
        for (int i = 2; i < Width; i++)
        {
            var cell = Get(i, et.PositionCurrent.Y);

            if (cell.Type == TileType.Ore || cell.OreValue > 0)
            {
                coord = cell;
                break;
            }
        }

        return coord;
    }

    public IEnumerable<Coordinate> FindNearestOreCellByEntity(Entity robot)
    {
        Dictionary<Coordinate, float> distanceTable = new Dictionary<Coordinate, float>();

        for (int i = robot.PositionCurrent.X - 4; i < robot.PositionCurrent.X + 4; i++)
        {
            for (int j = robot.PositionCurrent.Y - 4; j < robot.PositionCurrent.Y + 4; j++)
            {
                if (i > Width)
                {
                    i = Width;
                }

                if (j > Height)
                {
                    j = Height;
                }

                if (i < 0)
                {
                    i = 0;
                }

                if (j < 0)
                {
                    j = 0;
                }

                var cell = Get(i, j);

                if (cell != null && (cell.Type == TileType.Ore && cell.OreValue > 0))
                {
                    distanceTable[cell] = robot.PositionCurrent.Distance(cell);
                }
            }
        }

        return distanceTable.OrderByDescending(f=>f.Value).Select(f=>f.Key);
    }

    public IEnumerable<Entity> SortRadarDataByDensest(IEnumerable<Entity> radars)
    {
        Dictionary<Entity, int> varues = new Dictionary<Entity, int>();

        foreach(var radar in radars)
        {
            var sum = DoSummation(radar);
            if (sum != 0)
            {
                varues[radar] = sum;
            }
        }

        return varues.OrderByDescending(f => f.Value).Select(f => f.Key);
    }

    private int DoSummation(Entity radar)
    {
        var sigma = 0;

        for (int i = radar.PositionCurrent.X - 4; i < radar.PositionCurrent.X + 4; i++)
        {
            for (int j = radar.PositionCurrent.Y - 4; j < radar.PositionCurrent.Y + 4; j++)
            {
                if (i > Width)
                {
                    i = Width;
                }

                if (j > Height)
                {
                    j = Height;
                }

                if (i < 0)
                {
                    i = 0;
                }

                if (j < 0)
                {
                    j = 0;
                }

                var coordsItem = Get(i, j);

                if (coordsItem != null)
                {
                    sigma += coordsItem.OreValue;
                }
            }
        }

        return sigma;
    }

    public Coordinate FindNearestDenseOreCellByArea(Entity radar)
    {
        List<Coordinate> coords = new List<Coordinate>();

        for (int i = radar.PositionCurrent.X - 4; i < radar.PositionCurrent.X + 4; i++)
        {
            for (int j = radar.PositionCurrent.Y - 4; j < radar.PositionCurrent.Y + 4; j++)
            {
                if (i > Width)
                {
                    i = Width;
                }

                if (j > Height)
                {
                    j = Height;
                }

                if (i < 0)
                {
                    i = 0;
                }

                if (j < 0)
                {
                    j = 0;
                }

                var coordsItem = Get(i, j);

                if (coordsItem != null && coordsItem.OreValue > 0)
                {
                    coords.Add(coordsItem);
                }
            }
        }

        return coords.OrderByDescending(f => f.OreValue).FirstOrDefault();
    }

    public void UpdateRadarCd(int radarCooldown)
    {
        _isRadarReady = radarCooldown == 0;
    }

    public void Remove(int id)
    {
        _entities.Remove(id);
    }
}

public class Entity
{
    public int Id { get; private set; }
    public EntityType Type { get; private set; }
    public Coordinate PositionCurrent { get; private set; }
    public Coordinate PositionTarget { get; private set; }

    private string _lastCommand;
    private string _currentCommand;
    public string Command
    {
        get
        {
            return _currentCommand;
        }
        set
        {
            _currentCommand = _lastCommand = value;
        }
    }

    public Item Item { get; private set; }

    public Entity(int id, int type, Coordinate position, int item)
    {
        Id = id;
        Type = (EntityType)type;
        PositionCurrent = position;

        if (IsMyRobot)
        {
            Item = (Item)item;
        }
    }

    public bool IsMyRobot
    {
        get
        {
            return Type == EntityType.AllyRobot;
        }
    }

    public bool HasReachedTarget
    {
        get
        {
            return PositionCurrent.Equals(PositionTarget) ||
                   (Command.Contains("DIG") && IsAdjacentTo(PositionTarget));
        }
    }

    public Entity AssignedRadar { get; internal set; }

    public void Dig(Coordinate targetCell)
    {
        Command = $"DIG {targetCell.X} {targetCell.Y}";
        PositionTarget = targetCell;

        if (IsAdjacentTo(targetCell))
        {
            targetCell.Harvest();
        }
    }

    public bool IsAdjacentTo(Coordinate coordinate)
    {
        //check if you are adjacent
        return PositionCurrent.X == coordinate.X - 1 || //target is on the left
               PositionCurrent.X == coordinate.X + 1 || //target is on the right
               PositionCurrent.Y == coordinate.Y - 1 || //target is above
               PositionCurrent.Y == coordinate.Y + 1;   //target is below
    }

    public void DigGreedy(Map map)
    {
        var coords = map.FindNearestOreCellByWidth(this);

        if (coords != null)
        {
            Dig(coords);
        }
        else
        {
            // if at the end
            Dig(map.FindRadar2(this));
        }
    }

    public void Move(Coordinate coordinate)
    {
        Command = $"MOVE {coordinate.X} {coordinate.Y}";
        PositionTarget = coordinate;
    }

    public void MoveHorizontal(int steps)
    {
        Command = $"MOVE {PositionCurrent.X + steps} {PositionCurrent.Y}";
        PositionTarget = new Coordinate(PositionCurrent.X + steps, PositionCurrent.Y);
    }

    public void Wait()
    {
        Command = "WAIT";
    }

    public void AcquireRadar()
    {
        Command = "REQUEST RADAR";
        PositionTarget = new Coordinate(0, PositionCurrent.Y);
    }

    public bool AtHome()
    {
        return PositionCurrent.X == 0;
    }

    public float Distance(Entity entity)
    {
        return this.PositionCurrent.Distance(entity.PositionCurrent);
    }

    public void Update(Coordinate coordinate, int item)
    {
        if (!coordinate.Equals(PositionCurrent))
        {
            PositionCurrent = coordinate;
        }

        if (Item != (Item)item)
        {
            Item = (Item)item;
        }

        Command = null;
    }

    public void DoLastCommand()
    {
        Command = _lastCommand;
    }

    public void AssignRadar(Entity radarNear)
    {
        AssignedRadar = radarNear;
    }
}

public class Coordinate : IEquatable<Coordinate>
{
    public int X { get; }
    public int Y { get; }
    public TileType Type { get; private set; }
    public int OreValue { get; private set; }

    public Coordinate(int x, int y)
    {
        X = x;
        Y = y;
    }

    public void SetType(TileType type)
    {
        Type = type;
    }

    public void SetOreValue(int v)
    {
        OreValue = v;
    }

    public bool Equals(Coordinate other)
    {
        return X == other.X &&
               Y == other.Y;
    }

    public void Harvest()
    {
        OreValue--;
    }

    public float Distance(Coordinate other)
    {
        return (float)
            Math.Sqrt(
                Math.Abs(
                    Math.Pow((Y - other.Y), 2) +
                    Math.Pow((X - other.X), 2)));
    }

}

public enum EntityType
{
    AllyRobot = 0,
    HostileRobot = 1,
    BuriedRadar = 2,
    BuriedTrap = 3
}

public enum Item
{
    None = -1,
    Radar = 2,
    Trap = 3,
    Ore = 4
}

public enum TileType
{
    Ore,
    Hole,
    Nothing
}