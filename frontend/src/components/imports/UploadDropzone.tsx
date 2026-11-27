import { useRef, useState } from 'react';
import { Upload } from 'lucide-react';
import { clsx } from 'clsx';
import { Button } from '../ui/Button';

interface UploadDropzoneProps {
  onFile: (file: File) => void;
  isLoading: boolean;
  error?: string | null;
}

export const UploadDropzone = ({ onFile, isLoading, error }: UploadDropzoneProps) => {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);

  const accept = (file: File | undefined) => {
    if (!file) return;
    const name = file.name.toLowerCase();
    if (!name.endsWith('.csv') && !name.endsWith('.pdf')) {
      setLocalError('Only .csv or .pdf files are supported.');
      return;
    }
    setLocalError(null);
    onFile(file);
  };

  const shown = localError ?? error;

  return (
    <div>
      <div
        data-testid="upload-dropzone"
        onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
        onDragLeave={() => setDragging(false)}
        onDrop={(e) => { e.preventDefault(); setDragging(false); accept(e.dataTransfer.files[0]); }}
        className={clsx(
          'rounded-2xl border-2 border-dashed px-6 py-10 text-center transition-colors',
          dragging ? 'border-brand-500 bg-brand-50' : 'border-gray-300 bg-white'
        )}
      >
        <Upload className="h-10 w-10 text-gray-300 mx-auto mb-3" />
        <p className="text-gray-700 font-medium">Drop a monthly statement here</p>
        <p className="text-sm text-gray-400 mt-1">CSV exports from CIBC, TD, MBNA, RBC or Wealthsimple, or a CIBC credit card PDF statement.</p>
        <input
          ref={inputRef}
          type="file"
          accept=".csv,.pdf,text/csv,application/pdf"
          aria-label="Statement file"
          className="hidden"
          onChange={(e) => { accept(e.target.files?.[0]); e.target.value = ''; }}
        />
        <Button className="mt-4" isLoading={isLoading} onClick={() => inputRef.current?.click()}>
          Choose file
        </Button>
      </div>
      {shown && <p role="alert" className="text-sm text-red-600 mt-2">{shown}</p>}
    </div>
  );
};
