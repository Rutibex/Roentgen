# ADR 0016: Generate room topology before structure geometry

Status: Accepted

Roentgen generates semantic rooms, connections, route classes and reservations before walls or meshes. Geometry is a compiled product of the validated topology. This makes reachability, quest binding and deterministic repair possible without reverse-engineering rendered triangles.
