using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

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

                    if(ore == "?")
                    {
                        coord.SetType(TileType.Ore);
                    }

                    if(hole == 1)
                    {
                        coord.SetType(TileType.Hole);
                        if (int.TryParse(ore, out int val))
                        {
                            coord.SetOreValue(val);
                        }
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

                var eyy = new Entity(id, type, map.Get(x, y), item);

                map.UpdateEntity(eyy);

            }

            //Console.Error.WriteLine(map.Entities.Count());
            //map.ProcessGreedy();
            map.ProcessSearchBased();
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
    List<Coordinate> _vectors = new List<Coordinate>();
    Dictionary<int, Entity> _entities = new Dictionary<int, Entity>();
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
    public bool CanRequestRadar { get; private set; }

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

    public void UpdateEntity(Entity entity)
    {
        _entities[entity.Id] = entity;
    }

    public void ProcessGreedy()
    {
        foreach (var et in MyRobots)
        {
            if(et.Item == Item.Ore)
            {
                //run back
                et.Move(Get(0, et.Position.Y));
                continue;
            }

            et.DigGreedy(this);
        }
    }

    private int _track = -1;
    private static Random randomNumber = new Random();
    public int GetRandomStuff()
    {
        if (_track == -1)
        {
            _track = randomNumber.Next(1, 4);
        }

        return _track;
    }

    public void ProcessSearchBased()
    {
        //we need 1 robots with radar
        //first and last will implent greedy
        for (int i = 0; i < MyRobots.Count(); i++)
        {
            var entity = MyRobots.ElementAt(i);

            //mid boi, pick up the radar and drop it in the middle
            if (entity.AtHome() && CanRequestRadar)
            {
                entity.AcquireRadar();
                CanRequestRadar = false;
            }
            else if (entity.Item == Item.Radar)
            {
                //drop it in the middle of quadrant
                var coords = GetRandomStuff();

                switch (coords)
                {
                    case 1:
                        Console.Error.WriteLine("PUT RADAR IN A MALL. 1");
                        var mp = new Coordinate(30 / 4, 15 / 4);
                        entity.Dig(mp);
                        if(entity.Position.Equals(mp))
                        {
                            _track = -1;
                        }
                        break;
                    case 2:
                        Console.Error.WriteLine("PUT RADAR IN A MALL. 2");
                        var mp2 = new Coordinate(30 - 30 / 4, 15 / 4);
                        entity.Dig(mp2);
                        if(entity.Position.Equals(mp2))
                        {
                            _track = -1;
                        }
                        break;
                    case 3:
                        Console.Error.WriteLine("PUT RADAR IN A MALL. 3");
                        var mp3 = new Coordinate(30 - 30 / 4, 15 / 4);
                        entity.Dig(mp3);
                        if(entity.Position.Equals(mp3))
                        {
                            _track = -1;
                        }

                        break;
                    case 4:
                        Console.Error.WriteLine("PUT RADAR IN A MALL. 4");
                        var mp4 = new Coordinate(30 - 30 / 4, 15 - 15 / 4);
                        entity.Dig(mp4);
                        if(entity.Position.Equals(mp4))
                        {
                            _track = -1;
                        }
                        break;
                }
            }
            else if (entity.Item == Item.None)
            {
                //find nearest radar to et
                var radar = FindNearestRadar(entity);

                if (radar != null)
                {
                    var patchOfMinerals = FindNearestDenseOreCellByArea(radar);

                    if (patchOfMinerals != null)
                    {
                        entity.Dig(patchOfMinerals);
                    }

                }

                else
                {

                    entity.DigGreedy(this);
                }
            }
            else if (entity.Item == Item.Ore)
            {
                entity.MoveHorizontal(-entity.Position.X);
            }
        }
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
        Console.Error.WriteLine($"Dist. Entity {entity.Id} : {minDist} to radar {retVal?.Id}");
        return retVal;
    }

    public Coordinate FindNearestOreCellByWidth(Entity et)
    {
        Coordinate coord = null;
        for (int i = 2; i < Width; i++)
        {
            var cell = Get(i, et.Position.Y);

            if (cell.Type == TileType.Ore || cell.OreValue > 0)
            {
                coord = cell;
                break;
            }
        }

        return coord;
    }

    public Coordinate FindNearestDenseOreCellByArea(Entity radar)
    {
        List<Coordinate> coords = new List<Coordinate>();

        for (int i = radar.Position.X - 4; i < radar.Position.X + 4; i++)
        {
            for (int j = radar.Position.Y - 4; j < radar.Position.Y; j++)
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

                if (coordsItem.Type == TileType.Ore)
                {
                    coords.Add(coordsItem);
                }
            }
        }

        return coords.OrderByDescending(f => f.OreValue).FirstOrDefault();
    }

    public void UpdateRadarCd(int radarCooldown)
    {
        CanRequestRadar = radarCooldown == 0;
        //Console.Error.WriteLine(CanRequestRadar);
    }
}

public class Entity
{
    public int Id { get; private set; }
    public EntityType Type { get; private set; }
    public Coordinate Position { get; private set; }
    public string Command { get; private set; }

    public Item Item { get; private set; }

    public Entity(int id, int type, Coordinate position, int item)
    {
        Id = id;
        Type = (EntityType)type;
        Position = position;

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

    public void Dig(Coordinate nearestOreCell)
    {
        Command = $"DIG {nearestOreCell.X} {nearestOreCell.Y}";
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
            Wait();
        }
    }

    public void Move(Coordinate coordinate)
    {
        Command = $"MOVE {coordinate.X} {coordinate.Y}";
    }

    public void MoveHorizontal(int steps)
    {
        Command = $"MOVE {Position.X + steps} {Position.Y}";
    }

    public void Wait()
    {
        Command = "WAIT";
    }

    public int AcquireRadar()
    {
        Command = "REQUEST RADAR";
        return 1;
    }

    public bool AtHome()
    {
        return Position.X == 0;
    }

    public float Distance(Entity entity)
    {
        return (float)
            Math.Sqrt(
                Math.Abs(
                    Math.Pow((Position.Y - entity.Position.Y), 2) +
                    Math.Pow((Position.X - entity.Position.X), 2)));
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