import sys
import json
import bpy
import os
import sys


def render_and_save(animation=False):
    if animation:
        # Render the animation
        bpy.context.scene.render.filepath = bpy.path.abspath(
            "//../Resources/VideoBuffer.mp4"
        )

        bpy.context.scene.render.image_settings.file_format = "FFMPEG"
        bpy.context.scene.render.ffmpeg.format = "MPEG4"

        bpy.ops.render.render(animation=True)
        print(f"Render Video saved to {bpy.context.scene.render.filepath }")
    else:
        # Render the image
        bpy.context.scene.render.filepath = bpy.path.abspath(
            "//../Resources/PreviewBuffer.png"
        )
        bpy.context.scene.render.image_settings.file_format = "PNG"
        bpy.ops.render.render(write_still=True)
        print(f"Render Imagesaved to {bpy.context.scene.render.filepath }")


def import_model(filepath):
    # Get the extension of the file
    extension = os.path.splitext(filepath)[1].lower()

    if extension == ".obj":
        # Import OBJ
        bpy.ops.import_scene.obj(filepath=filepath)
    elif extension == ".fbx":
        # Import FBX
        bpy.ops.import_scene.fbx(filepath=filepath)
    elif extension == ".glb" or extension == ".gltf":
        # Import GLB/GLTF
        bpy.ops.import_scene.gltf(filepath=filepath)
    elif extension == ".blend":
        with bpy.data.libraries.load(filepath, link=False) as (data_from, data_to):
            data_to.objects = data_from.objects
        # Link the object to the current scene
        for obj in data_to.objects:
            if obj is not None:
                bpy.context.collection.objects.link(obj)
    elif extension in [".usd", ".usdz", ".usdc"]:
        # Import USD, USDZ, or USDC
        bpy.ops.wm.usd_import(filepath=filepath)

    else:
        print("Unsupported file format")

    # append imported models to collection named "Model"
    assign_objects_to_collection(bpy.context.selected_objects, "Model")


def assign_objects_to_collection(objects, collection_name):
    """
    Moves a list of objects to a specified collection. Creates the collection if it does not exist.
    This function ensures objects are only linked to the specified collection.

    Parameters:
    - objects: List of Blender object references to be moved.
    - collection_name: Name of the collection the objects will be moved to.
    """

    # Check if the collection exists, if not create it
    if collection_name not in bpy.data.collections:
        new_collection = bpy.data.collections.new(name=collection_name)
        bpy.context.scene.collection.children.link(new_collection)
    else:
        new_collection = bpy.data.collections[collection_name]

    # Loop through the objects and move them to the new collection
    for obj in objects:
        # Unlink the object from all collections it's currently linked to
        for col in obj.users_collection:
            col.objects.unlink(obj)

        # Now link the object to the new collection
        new_collection.objects.link(obj)


def set_latest_keyframe_to_frame(obj, frame_number):
    """
    Set the latest keyframe of the given object to a specific frame number
    and adjust the scene's end frame accordingly.

    Parameters:
    obj (bpy.types.Object): The object to modify the keyframes of.
    frame_number (int): The frame number to set the latest keyframe to.
    """
    # Ensure the object is not None and has animation data
    if obj and obj.animation_data and obj.animation_data.action:
        # Initialize variable to keep track of the latest keyframe
        latest_keyframe_frame = 0

        # Iterate through all the fcurves in the object's animation data
        for fcurve in obj.animation_data.action.fcurves:
            # Check each keyframe point in the fcurve
            for keyframe_point in fcurve.keyframe_points:
                # Update the latest_keyframe variable if this keyframe is later than the current latest
                if keyframe_point.co.x > latest_keyframe_frame:
                    latest_keyframe_frame = keyframe_point.co.x
        # If a latest keyframe has been found
        if latest_keyframe_frame > 0:
            # Calculate the difference to move all keyframes so that the latest matches the desired frame number
            frame_difference = frame_number - latest_keyframe_frame

            # Apply the difference to all keyframes
            for fcurve in obj.animation_data.action.fcurves:
                for keyframe_point in fcurve.keyframe_points:
                    if keyframe_point.co.x == latest_keyframe_frame:
                        keyframe_point.co.x += frame_difference

            # Set the scene's end frame to at least the specified frame number
            bpy.context.scene.frame_end =  frame_number

            print(
                f"Latest keyframe set to frame {frame_number}. Scene end frame set to {bpy.context.scene.frame_end}."
            )
        else:
            print("No keyframes found in object.")
    else:
        print("Object has no animation data or is None.")


def setup_animation(frame_end):
    # get subject object
    subject = bpy.data.objects.get("subject")
    if subject is not None:
        set_latest_keyframe_to_frame(subject, frame_end)

    else:
        print("No subject object found")
        return


def setLighting(data):
    for collection in bpy.data.collections["Lighting"].children:
        if collection.name == data["lighting"]:
            collection.hide_render = False
            collection.hide_viewport = False
        else:
            collection.hide_viewport = True
            collection.hide_render = True


def set_resolution(width, height):
    # set resolution make sure even numbers are used, if not fix it
    if data["width"] % 2 != 0:
        data["width"] += 1
    if data["height"] % 2 != 0:
        data["height"] += 1

    bpy.context.scene.render.resolution_x = width
    bpy.context.scene.render.resolution_y = height


def set_quality(quality):
    # set quality
    if data["quality"] == "Low":
        # enable eevee
        bpy.context.scene.render.engine = "BLENDER_EEVEE"
        bpy.context.scene.render.image_settings.quality = 25
    elif data["quality"] == "Medium":
        # enable CYCLES
        bpy.context.scene.render.engine = "CYCLES"
        bpy.context.scene.cycles.samples = 30
    elif data["quality"] == "High":
        # enable cycles
        bpy.context.scene.render.engine = "CYCLES"
        # set samples to 100
        bpy.context.scene.cycles.samples = 100
    else:
        print("Invalid quality setting")
        sys.exit(1)



def set_rgb_node_color(material_name, color):
    """
    Find the RGB node in the specified material and set its color.
    
    Args:
    - material_name (str): The name of the material to search within.
    - color (tuple): A tuple of three floats representing the RGB values (range 0-1).
    """
    # Ensure the material exists
    material = bpy.data.materials.get(material_name)
    if not material:
        print(f"Material '{material_name}' not found.")
        return
    
    # Ensure the material uses nodes
    if not material.use_nodes:
        print(f"Material '{material_name}' does not use nodes.")
        return
    
    # Get the node tree of the material
    nodes = material.node_tree.nodes
    
    # Search for the RGB node
    rgb_node = next((node for node in nodes if node.type == 'RGB'), None)
    if rgb_node:
        rgb_node.outputs[0].default_value = (*color, 1)  # Set color, keep alpha as 1
        print(f"RGB node color set to {color} in material '{material_name}'.")
    else:
        print(f"No RGB node found in material '{material_name}'.")

def set_background_color(data):
    # Set the background color of the scene
    if "backgroundColor" in data:
        color = data["backgroundColor"]
        if color == "Black":
            #set black    
            set_rgb_node_color("background", (0, 0, 0))
            print(f"Background color set to {color}.")
            return
        elif color == "Green":
            #set green
            set_rgb_node_color("background", (0, 1, 0))
            print("Invalid background color setting.")

        elif color == "White":
            #set white
            set_rgb_node_color("background", (1, 1, 1))
            print("Invalid background color setting.")
            return
        
# Get the latest argument
latest_argument = sys.argv[-1]

# Convert to dict using json library
try:
    data = json.loads(latest_argument)
    print(data)
except json.JSONDecodeError:
    print("Invalid JSON format")
    sys.exit(1)

# Import the model
import_model(data["fileName"])

# check if the data dict has key named "lighting"


if all(
    key in data
    for key in ["lighting", "mode", "fps", "duration", "width", "height", "quality"]
):
    setLighting(data)
    print("Animation Mode")
    # set fps
    bpy.context.scene.render.fps = int(data["fps"])

    set_resolution(int(data["width"]), int(data["height"]))

    # set duration of animation
    setup_animation(int(data["fps"] * data["duration"]))

    # set the quality
    set_quality(data["quality"])

    set_background_color(data)

    # set render type to mp4
    bpy.context.scene.render.image_settings.file_format = "FFMPEG"

    # set render filepath as //VideoBuffer.mp4
    bpy.context.scene.render.filepath = "//../Resources/VideoBuffer.mp4"
    # render and save animation
    render_and_save(animation=True)
    # sys.exit(0)
else:
    print("Photo Mode")
    # render and save single frame
    render_and_save(animation=False)
    # sys.exit(0)
