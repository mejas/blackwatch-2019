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

                    if (ore == "?")
                    {
                        coord.SetType(TileType.Ore);
                    }
                    else if (hole == 1)
                    {
                        coord.SetType(TileType.Hole);
                    }
                    else if(ore != "?")
                    {
                        coord.SetType(TileType.Ore);
                        coord.SetOreValue(int.Parse(ore));
                    }
                }
            }

            inputs = Console.ReadLine().Split(' ');
            int entityCount = int.Parse(inputs[0]); // number of entities visible to you
            int radarCooldown = int.Parse(inputs[1]); // turns left until a new radar can be requested
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

                map.UpdateEntity(new Entity(id, type, map.Get(x, y), item));

            }

            Console.Error.WriteLine(map.Entities.Count());
            map.ProcessGreedy();
            foreach (var et in map.Entities.Where(e => e.IsRobot))
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
    public IEnumerable<Entity> Entities
    {
        get
        {
            return _entities.Values;
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
        foreach (var entity in _entities)
        {
            var et = entity.Value;

            if (et.IsRobot)
            {
                if (et.Item != Item.Ore)
                {
                    //get the nearest ore cell
                    var nearestOreCell = FindNearestOreCell(et);
                    //dig and retreat
                    if (nearestOreCell != null)
                    {
                        et.Dig(nearestOreCell);
                    }
                    else
                    {
                        et.Move(Get(Width, et.Position.Y));
                    }
                }
                else
                {
                    et.Move(Get(0, et.Position.Y));
                }
            }
        }
    }

    private Coordinate FindNearestOreCell(Entity et)
    {
        Coordinate coord = null;
        for (int i = et.Position.X; i < Width; i++)
        {
            var cell = Get(i, et.Position.Y);

            Console.Error.WriteLine($"found ore in {cell.X} {cell.Y} with {cell.Type}");
            if (cell.Type == TileType.Ore)
            {
                coord = cell;
                break;
            }
        }

        return coord;
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

        if (IsRobot)
        {
            Item = (Item)item;
        }
    }

    public bool IsRobot
    {
        get
        {
            return Type == (EntityType.AllyRobot | EntityType.HostileRobot);
        }
    }

    public void Dig(Coordinate nearestOreCell)
    {
        Command = $"DIG {nearestOreCell.X - 1} {nearestOreCell.Y}";
    }

    public void Move(Coordinate coordinate)
    {
        Command = $"MOVE {coordinate.X} {coordinate.Y}";
    }
}

public class Coordinate
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
        if (Type == TileType.Ore)
        {
            OreValue = v;
        }
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