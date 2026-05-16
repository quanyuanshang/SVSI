import { useEffect, useMemo, useState } from "react";
import {
  getPortraitDisplaySize,
  useStardewAssetResolver,
} from "../lib/stardewAssets";
import { translateCharacter } from "../lib/translations";
import { PortraitFrameView } from "./PortraitFrameView";

interface CharacterPortraitProps {
  name?: string | null;
  sourceModId?: string | null;
  size?: "sm" | "md" | "lg";
  label?: string;
  /** Stardew portrait sheet expression index; default 0 (neutral). */
  expressionIndex?: number;
}

export function CharacterPortrait({
  name,
  sourceModId,
  size = "md",
  label,
  expressionIndex = 0,
}: CharacterPortraitProps) {
  const resolver = useStardewAssetResolver();
  const displayName = label ?? (name ? translateCharacter(name, sourceModId).zh : "Farmer");
  const initials = buildInitials(displayName);
  const resolvedPortrait = useMemo(
    () => resolver.resolvePortrait(name, sourceModId),
    [name, resolver, sourceModId],
  );
  const candidates = buildPortraitCandidates(name, sourceModId, resolvedPortrait?.hdUrl);
  const [candidateIndex, setCandidateIndex] = useState(0);
  const [imageFailed, setImageFailed] = useState(false);
  const hdSheetUrl = !imageFailed ? candidates[candidateIndex] : undefined;
  const displaySize = getPortraitDisplaySize(size);

  useEffect(() => {
    setCandidateIndex(0);
    setImageFailed(false);
  }, [resolvedPortrait?.hdUrl, name, sourceModId]);

  return (
    <span
      aria-label={displayName}
      className={`portrait portrait--${size}${hdSheetUrl ? " portrait--image" : ""}`}
      title={name ?? displayName}
    >
      {hdSheetUrl ? (
        <>
          <PortraitFrameView
            baseSheetUrl={resolvedPortrait?.baseUrl}
            displaySize={displaySize}
            expressionIndex={expressionIndex}
            gridOverride={resolvedPortrait?.gridOverride}
            hdSheetUrl={hdSheetUrl}
          />
          <img
            alt=""
            className="portrait__probe"
            draggable={false}
            onError={() => {
              const nextIndex = candidateIndex + 1;
              if (nextIndex < candidates.length) {
                setCandidateIndex(nextIndex);
              } else {
                setImageFailed(true);
              }
            }}
            src={hdSheetUrl}
          />
        </>
      ) : (
        <span className="portrait__face">{initials}</span>
      )}
    </span>
  );
}

export function CharacterPortraitStack({
  names,
  sourceModId,
  includeFarmer = false,
  max = 4,
  expressionIndex = 0,
}: {
  names: string[];
  sourceModId?: string | null;
  includeFarmer?: boolean;
  max?: number;
  expressionIndex?: number;
}) {
  const displayNames = includeFarmer ? ["__farmer__", ...names] : names;
  const visible = displayNames.slice(0, max);
  const overflow = displayNames.length - visible.length;

  return (
    <span className="portrait-stack">
      {visible.map((portraitName) => (
        <CharacterPortrait
          key={portraitName}
          expressionIndex={expressionIndex}
          label={portraitName === "__farmer__" ? "Farmer" : undefined}
          name={portraitName === "__farmer__" ? null : portraitName}
          size="sm"
          sourceModId={sourceModId}
        />
      ))}
      {overflow > 0 ? <span className="portrait-overflow">+{overflow}</span> : null}
    </span>
  );
}

function buildInitials(value: string): string {
  const trimmed = value.trim();
  if (!trimmed) {
    return "??";
  }

  const asciiWords = trimmed.match(/[A-Za-z0-9]+/g);
  if (asciiWords?.length) {
    return asciiWords
      .slice(0, 2)
      .map((word) => word[0])
      .join("")
      .toUpperCase();
  }

  return Array.from(trimmed).slice(0, 2).join("");
}

function buildPortraitCandidates(
  name?: string | null,
  sourceModId?: string | null,
  generatedPortrait?: string | null,
): string[] {
  const rawName = name?.trim();
  if (!rawName) {
    return unique([generatedPortrait, "/portraits/farmer.png", "/portraits/Farmer.png"].filter(Boolean) as string[]);
  }

  const normalizedName = normalizeFileSegment(rawName);
  const nameCandidates = unique([
    rawName,
    rawName.toLowerCase(),
    normalizedName,
    normalizedName.toLowerCase(),
  ]);
  const modSegment = sourceModId ? normalizeFileSegment(sourceModId) : null;
  const paths: string[] = generatedPortrait ? [generatedPortrait] : [];

  for (const candidate of nameCandidates) {
    if (modSegment) {
      paths.push(`/portraits/${modSegment}/${candidate}.png`);
      paths.push(`/portraits/${modSegment}/${candidate}.webp`);
    }
    paths.push(`/portraits/${candidate}.png`);
    paths.push(`/portraits/${candidate}.webp`);
  }

  return unique(paths);
}

function normalizeFileSegment(value: string): string {
  return value
    .trim()
    .replace(/[^A-Za-z0-9_.-]+/g, "_")
    .replace(/^_+|_+$/g, "");
}

function unique(values: string[]): string[] {
  return Array.from(new Set(values.filter(Boolean)));
}
