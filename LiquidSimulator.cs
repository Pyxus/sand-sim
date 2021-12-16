﻿using Godot;

namespace FluidSim
{
    public class LiquidSimulator : Reference
    {
        private float _maxLiquidAmount = 1.0f;
        private float _minLiquidAmount = 0.01f;
        private float _maxLiquidCompression = 0.25f;
        private float _minFlow = 0.01f;
        private float _maxFlow = 5f;
        private float _flowSpeed = 1.0f;
        private float[,] _flowChange;
        public Cell[,] Cells;

        public void Initialize(int size)
        {
            _flowChange = new float[size, size];
            Cells = PopulateCells(size);
            UpdateNeighbors();
        }

        public void Process()
        {
            ResetFlowChanges();

            // Main loop
            for (int x = 0; x < Cells.GetLength(0); x++)
            {
                for (int y = 0; y < Cells.GetLength(1); y++)
                {
                    var cell = Cells[x, y];
                    var c = (x, y);

                    // Skip cell if no flow should occur
                    if (cell.Type == Cell.CellType.Solid || cell.LiquidAmount < _minLiquidAmount || cell.LiquidAmount == 0)
                    {
                        cell.LiquidAmount = 0;
                        continue;
                    }

                    if (cell.IsSettled)
                        continue;

                    var initialLiquid = cell.LiquidAmount;
                    var remainingLiquid = cell.LiquidAmount;

                    // Flow to bottom cell
                    float flow = 0;
                    if (cell.Bottom != null && cell.Bottom.Type == Cell.CellType.Liquid)
                    {
                        // Determine rate of flow
                        flow = CalcVFlow(cell.LiquidAmount, cell.Bottom.LiquidAmount) - cell.Bottom.LiquidAmount;
                        if (cell.Bottom.LiquidAmount > 0 && flow > _minFlow)
                            flow *= _flowSpeed;

                        // Constrain flow
                        flow = Mathf.Clamp(Mathf.Max(flow, 0), 0, Mathf.Min(_maxFlow, cell.LiquidAmount));

                        // Update temp values
                        if (flow != 0)
                        {
                            remainingLiquid -= flow;
                            _flowChange[x, y] -= flow;
                            _flowChange[x, y + 1] += flow;
                            cell.Bottom.IsSettled = false;
                        }

                        if (remainingLiquid < _minLiquidAmount)
                        {
                            _flowChange[x, y] -= remainingLiquid;
                            continue;
                        }
                    }

                    // Flow to left cell
                    if (cell.Left != null && cell.Left.Type == Cell.CellType.Liquid)
                    {
                        // Calculate flow rate
                        flow = (remainingLiquid - cell.Left.LiquidAmount) / 4f;
                        if (flow > _minFlow)
                            flow *= _flowSpeed;

                        // constrain flow
                        flow = Mathf.Clamp(Mathf.Max(flow, 0), 0, Mathf.Min(_maxFlow, remainingLiquid));

                        // Adjust temp values
                        if (flow != 0)
                        {
                            remainingLiquid -= flow;
                            _flowChange[x, y] -= flow;
                            _flowChange[x - 1, y] += flow;
                            cell.Left.IsSettled = false;
                        }

                        if (remainingLiquid < _minLiquidAmount)
                        {
                            _flowChange[x, y] -= remainingLiquid;
                            continue;
                        }
                    }

                    // Flow to right cell
                    if (cell.Right != null && cell.Right.Type == Cell.CellType.Liquid)
                    {
                        // calc flow rate
                        flow = (remainingLiquid - cell.Right.LiquidAmount) / 3f;
                        if (flow > _minFlow)
                            flow *= _flowSpeed;

                        // constrain flow
                        flow = Mathf.Clamp(Mathf.Max(flow, 0), 0, Mathf.Min(_maxFlow, remainingLiquid));

                        // Adjust temp values
                        if (flow != 0)
                        {
                            remainingLiquid -= flow;
                            _flowChange[x, y] -= flow;
                            _flowChange[x + 1, y] += flow;
                            cell.Right.IsSettled = false;
                        }

                        if (remainingLiquid < _minLiquidAmount)
                        {
                            _flowChange[x, y] -= remainingLiquid;
                            continue;
                        }
                    }
                    
                    if (cell.Top != null && cell.Top.Type == Cell.CellType.Liquid)
                    {
                        flow = remainingLiquid - CalcVFlow(remainingLiquid, cell.Top.LiquidAmount);
                        if (flow > _minFlow)
                            flow *= _flowSpeed;

                        // constrain flow
                        flow = Mathf.Clamp(Mathf.Max(flow, 0), 0, Mathf.Min(_maxFlow, remainingLiquid));

                        // Adjust values
                        if (flow != 0)
                        {
                            remainingLiquid -= flow;
                            _flowChange[x, y] -= flow;
                            _flowChange[x, y - 1] += flow;
                            cell.Top.IsSettled = false;
                        }

                        if (remainingLiquid < _minLiquidAmount)
                        {
                            _flowChange[x, y] -= remainingLiquid;
                            continue;
                        }
                    }
                    
                    if (Mathf.Abs(initialLiquid - remainingLiquid) < 0.0001f)
                    {
                        cell.SettleCount++;
                        if (cell.SettleCount >= 5)
                        {
                            cell.IsSettled = true;
                        }
                    }
                    else
                    {
                        cell.UnsettleNeighbors();
                    }
                }
            }

            UpdateCells();
        }

        private bool Flow((int x, int y) from, (int x, int y) to, ref float remainingLiquid, bool isVerticalFlow, bool isBottomFlow)
        {
            if (from.x >= Cells.GetLength(0) || from.y >= Cells.GetLength(1)) return true;
            if (to.x >= Cells.GetLength(0) || to.y >= Cells.GetLength(1)) return true;

            var flow = 0f;
            var fromCell = Cells[from.x, from.y];
            var toCell = Cells[to.x, to.y];

            if (toCell == null || toCell.Type != Cell.CellType.Liquid) return true;
            
            // Determine rate of flow
            if (isVerticalFlow)
            {
                if (isBottomFlow)
                {
                    flow = CalcVFlow(fromCell.LiquidAmount, toCell.LiquidAmount) - toCell.LiquidAmount;
                    if (toCell.LiquidAmount > 0 && flow > _minFlow)
                    {
                        flow *= _flowSpeed;
                    }
                }
                else
                {
                    flow = remainingLiquid - CalcVFlow(remainingLiquid, toCell.LiquidAmount);
                    if (flow > _minFlow)
                    {
                        flow *= _flowSpeed;
                    }
                }
                    
            }
            else
            {
                flow = (remainingLiquid - toCell.LiquidAmount) / 4f;
                if (flow > _minFlow)
                    flow *= _flowSpeed;
            }

            // Constrain flow
            if (isBottomFlow)
            {
                flow = Mathf.Clamp(Mathf.Max(flow, 0), 0, Mathf.Min(_maxFlow, fromCell.LiquidAmount));
            }
            else
            {
                flow = Mathf.Clamp(Mathf.Max(flow, 0), 0, Mathf.Min(_maxFlow, remainingLiquid));
            }
            

            // Update temp values
            if (flow != 0)
            {
                remainingLiquid -= flow;
                _flowChange[from.x, from.y] -= flow;
                _flowChange[to.x, to.y + 1] += flow;
                toCell.IsSettled = false;
            }

            if (remainingLiquid < _minLiquidAmount)
            {
                _flowChange[to.x, to.y] -= remainingLiquid;
                return false;
            }

            return true;
        }
        
        private void UpdateNeighbors()
        {
            var width = Cells.GetLength(0);
            var height = Cells.GetLength(1);

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    if (x > 0)
                    {
                        Cells[x, y].Left = Cells[x - 1, y];
                    }

                    if (x < width - 1)
                    {
                        Cells[x, y].Right = Cells[x + 1, y];
                    }

                    if (y > 0)
                    {
                        Cells[x, y].Top = Cells[x, y - 1];
                    }

                    if (y < height - 1)
                    {
                        Cells[x, y].Bottom = Cells[x, y + 1];
                    }
                }
            }
        }

        private Cell[,] PopulateCells(int size)
        {
            var cells = new Cell[size, size];

            for (var i = 0; i < size; i++)
            {
                for (var j = 0; j < size; j++)
                {
                    var cell = new Cell();
                    if (j == 0 || j == size - 1 || i == 0 || i == size - 1)
                    {
                        cell.SetType(Cell.CellType.Solid);
                    }

                    cells[i, j] = cell;
                }
            }

            return cells;
        }

        private float CalcVFlow(float remainingLiquid, float destLiquid)
        {
            float sum = remainingLiquid + destLiquid;
            float value = 0;

            if (sum <= _maxLiquidAmount)
            {
                value = _maxLiquidAmount;
            }
            else if (sum < 2 * _maxLiquidAmount + _maxLiquidCompression)
            {
                value = (_maxLiquidAmount * _maxLiquidAmount + sum * _maxLiquidCompression) / (_maxLiquidAmount + _maxLiquidCompression);
            }
            else
            {
                value = (sum + _maxLiquidCompression) / 2f;
            }

            return value;
        }

        private void ResetFlowChanges()
        {
            for (int x = 0; x < Cells.GetLength(0); x++)
            {
                for (int y = 0; y < Cells.GetLength(1); y++)
                {
                    _flowChange[x, y] = 0;
                }
            }
        }

        private void UpdateCells()
        {
            for (int x = 0; x < Cells.GetLength(0); x++)
            {
                for (int y = 0; y < Cells.GetLength(1); y++)
                {
                    Cells[x, y].LiquidAmount += _flowChange[x, y];
                    if (Cells[x, y].LiquidAmount < _minLiquidAmount)
                    {
                        Cells[x, y].LiquidAmount = 0;
                        Cells[x, y].IsSettled = false;
                    }
                }
            }
        }
    }
}