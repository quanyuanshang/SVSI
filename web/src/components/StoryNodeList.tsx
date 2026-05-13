import { StoryNodeCard } from "./StoryNodeCard";
import type { StoryNodeEvaluation } from "../types/story";

interface StoryNodeListProps {
  nodes: StoryNodeEvaluation[];
  totalCount: number;
  selectedNodeId: string | null;
  conflictCountByNodeId: Record<string, number>;
  onSelectNode: (node: StoryNodeEvaluation) => void;
}

export function StoryNodeList({
  nodes,
  totalCount,
  selectedNodeId,
  conflictCountByNodeId,
  onSelectNode,
}: StoryNodeListProps) {
  return (
    <section className="panel story-node-list">
      <div className="panel-heading">
        <h2>Story Nodes</h2>
        <p>
          showing {nodes.length} / total {totalCount}
        </p>
      </div>

      {nodes.length === 0 ? (
        <p className="empty-state">No story nodes available.</p>
      ) : (
        <div className="story-node-list__items">
          {nodes.map((node) => (
            <StoryNodeCard
              key={node.nodeId ?? `${node.eventId}-${node.sourceModId}`}
              node={node}
              selected={node.nodeId === selectedNodeId}
              conflictCount={node.nodeId ? (conflictCountByNodeId[node.nodeId] ?? 0) : 0}
              onSelect={onSelectNode}
            />
          ))}
        </div>
      )}
    </section>
  );
}
