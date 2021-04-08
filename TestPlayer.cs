using Godot;
using System;

public partial class TestPlayer : KinematicBody3D
{
    // How fast the player moves in meters per second.
    [Export]
    public int Speed = 14;
    // The downward acceleration when in the air, in meters per second squared.
    [Export]
    public int FallAcceleration = 75;

    private Vector3 _velocity = Vector3.Zero;

    // Declare member variables here. Examples:
    // private int a = 2;
    // private string b = "text";

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {

    }

    //  // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _PhysicsProcess(float delta)
    {
        // We create a local variable to store the input direction.
        var direction = Vector3.Zero;

        // We check for each move input and update the direction accordingly
        if (Input.IsActionPressed("move_right"))
        {
            direction.x += 1f;
        }
        if (Input.IsActionPressed("move_left"))
        {
            direction.x -= 1f;
        }
        if (Input.IsActionPressed("move_back"))
        {
            // Notice how we are working with the vector's x and z axes.
            // In 3D, the XZ plane is the ground plane.
            direction.z += 1f;
        }
        if (Input.IsActionPressed("move_forward"))
        {
            direction.z -= 1f;
        }

        if (direction != Vector3.Zero)
        {
            direction = direction.Normalized();
            GetNode<Node3D>("Pivot").LookAt(Translation + direction, Vector3.Up);
        }


        // Ground velocity
        _velocity.x = direction.x * Speed;
        _velocity.z = direction.z * Speed;
        // Vertical velocity
        _velocity.y -= FallAcceleration * delta;
        // Moving the character
        _velocity = MoveAndSlide(_velocity, Vector3.Up);

    }
}
