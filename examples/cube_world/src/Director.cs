namespace SiaGodot.CubeWorld;

using Godot;

using Sia;

public partial record struct GodotNode3D([SiaProperty] Node3D Node);
public partial record struct Position([SiaProperty] Vector3 Value);
public partial record struct Rotation([SiaProperty] Quaternion Value) {
    public Rotation() : this(Quaternion.Identity) {}
}
public partial record struct Mover([SiaProperty] float Speed);
public partial record struct Rotator([SiaProperty] Vector3 AngularSpeed);

public sealed class GodotFrame : IAddon
{
    public float Delta { get; set; }
    public float Time { get; set; }
}

public sealed class GodotNode3DPositionUpdateSystem : SystemBase
{
    public GodotNode3DPositionUpdateSystem()
    {
        Matcher = Matchers.From<TypeUnion<GodotNode3D, Position>>();
        Trigger = new EventUnion<WorldEvents.Add, Position.SetValue>();
    }

    public override void Execute(World world, Scheduler scheduler, IEntityQuery query)
    {
        query.ForEach(static entity => {
            entity.Get<GodotNode3D>().Node.Position = entity.Get<Position>().Value;
        });
    }
}

public sealed class GodotNode3DRotationUpdateSystem : SystemBase
{
    public GodotNode3DRotationUpdateSystem()
    {
        Matcher = Matchers.From<TypeUnion<GodotNode3D, Rotation>>();
        Trigger = new EventUnion<WorldEvents.Add, Rotation.SetValue>();
    }

    public override void Execute(World world, Scheduler scheduler, IEntityQuery query)
    {
        query.ForEach(static entity => {
            entity.Get<GodotNode3D>().Node.Quaternion = entity.Get<Rotation>().Value;
        });
    }
}

public sealed class MoverUpdateSystem : SystemBase
{
    public MoverUpdateSystem()
    {
        Matcher = Matchers.From<TypeUnion<Mover, Position, Rotation>>();
    }

    public override void Execute(World world, Scheduler scheduler, IEntityQuery query)
    {
        query.ForEach(world, static (world, entity) => {
            ref var mover = ref entity.Get<Mover>();
            ref var pos = ref entity.Get<Position>();
            ref var rot = ref entity.Get<Rotation>();

            var frame = world.GetAddon<GodotFrame>();
            var newPos = pos.Value + Vector3.Forward * rot.Value * mover.Speed * frame.Delta;
            world.Modify(entity, new Position.SetValue(newPos));
        });
    }
}

public sealed class RotatorUpdateSystem : SystemBase
{
    public RotatorUpdateSystem()
    {
        Matcher = Matchers.From<TypeUnion<Rotator, Rotation>>();
    }

    public override void Execute(World world, Scheduler scheduler, IEntityQuery query)
    {
        query.ForEach(world, static (world, entity) => {
            ref var rotator = ref entity.Get<Rotator>();
            ref var rot = ref entity.Get<Rotation>();

            var frame = world.GetAddon<GodotFrame>();
            var newRot = Quaternion.FromEuler(rot.Value.GetEuler() + rotator.AngularSpeed * frame.Delta);
            world.Modify(entity, new Rotation.SetValue(newRot));
        });
    }
}

public static class TestObject
{
    public static EntityRef Create(World world, Node3D Parent, PackedScene Template, Vector3 position)
    {
        var instance = Template.Instantiate<Node3D>();
        Parent.AddChild(instance);

        return world.CreateInSparseHost(Tuple.Create(
            new GodotNode3D(instance),
            new Position(position),
            new Rotation(),
            new Mover(Random.Shared.NextSingle() * 200f),
            new Rotator(new(0f, (Random.Shared.Next(1) == 1 ? 1f : -1f) * MathF.Tau * (1 + Random.Shared.NextSingle()), 0f))
        ));
    }
}

public partial class Director : Node3D
{
    [Export] PackedScene? Template;

    private readonly World _world = new();
    private readonly Scheduler _scheduler = new();

    private readonly GodotFrame _frame;

    public Director()
    {
        _world.RegisterSystem<MoverUpdateSystem>(_scheduler);
        _world.RegisterSystem<RotatorUpdateSystem>(_scheduler);
        _world.RegisterSystem<GodotNode3DPositionUpdateSystem>(_scheduler);
        _world.RegisterSystem<GodotNode3DRotationUpdateSystem>(_scheduler);

        _frame = _world.AcquireAddon<GodotFrame>();
    }

    public override void _Ready()
    {
        for (int i = 0; i != 1000; ++i) {
            TestObject.Create(_world, this, Template!,
                new Vector3(
                    Random.Shared.NextSingle() * 100 - 50, 0,
                    Random.Shared.NextSingle() * 100 - 50));
        }
    }

    public override void _Process(double delta)
    {
        _frame.Delta = (float)delta;
        _frame.Time += _frame.Delta;
        _scheduler.Tick();
    }
}
